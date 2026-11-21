using System;
using System.Collections.Generic;
using UnityEngine;
#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
using InputTouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace GameInputLab
{
    /// <summary>
    /// On-screen virtual joystick driven by StickTracker. Reads touches from the Input System
    /// (EnhancedTouch) when it is installed and enabled, otherwise from the legacy Input Manager.
    /// Read <see cref="Value"/> each frame for movement (length 0–1, +y up).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Input Lab/Virtual Joystick")]
    public sealed class VirtualJoystick : MonoBehaviour
    {
        const int MouseTouchId = int.MinValue;

        [Tooltip("JSON exported from Game Input Lab. When set, it replaces the settings below on Awake.")]
        [SerializeField] TextAsset preset;

        [SerializeField] JoystickConfig config = new JoystickConfig();

        [Tooltip("Touch area. A touch must begin inside it to take the stick. Empty: the whole screen. " +
                 "In fixed mode the stick centre is the centre of this area.")]
        [SerializeField] RectTransform area;

        [Tooltip("Optional stick base. In floating mode it moves to where the finger lands.")]
        [SerializeField] RectTransform stickBase;

        [Tooltip("Optional knob. Follows the finger, limited to the radius.")]
        [SerializeField] RectTransform knob;

        [Tooltip("Screen pixels per point. 0: estimate from Screen.dpi / 160. " +
                 "Set it if the stick feels larger or smaller than in the app on a given device.")]
        [SerializeField, Min(0f)] float pixelsPerPointOverride;

        [Tooltip("In the Editor, drive the stick with the left mouse button when no touches are present.")]
        [SerializeField] bool mouseInEditor = true;

        readonly List<StickTouch> touches = new List<StickTouch>();
        readonly Vector3[] corners = new Vector3[4];
        StickTracker tracker;
        Canvas canvas;
        Vector3 baseRestPosition;
        bool mouseDown;

        /// <summary>Raised when the stick lets go, with the reason.</summary>
        public event Action<StickReleaseReason> Released;

        /// <summary>Smoothed stick output, length 0–1, +y up.</summary>
        public Vector2 Value => tracker != null ? tracker.Output : Vector2.zero;

        public bool IsPressed => tracker != null && tracker.IsPressed;

        public StickTracker Tracker => tracker;

        public JoystickConfig Config
        {
            get => config;
            set
            {
                config = value ?? new JoystickConfig();
                if (tracker != null) tracker.Config = config;
            }
        }

        /// <summary>Approximate pixels per point (iOS pt / Android dp) from the screen DPI.</summary>
        public static float EstimatePixelsPerPoint() => Screen.dpi > 0f ? Screen.dpi / 160f : 1f;

        public float PixelsPerPoint => pixelsPerPointOverride > 0f ? pixelsPerPointOverride : EstimatePixelsPerPoint();

        void Awake()
        {
            if (preset != null) config = JoystickConfig.FromJson(preset.text);
            tracker = new StickTracker(config);
            tracker.Released += reason => Released?.Invoke(reason);
            RectTransform reference = area != null ? area : stickBase != null ? stickBase : knob;
            if (reference != null)
            {
                Canvas parentCanvas = reference.GetComponentInParent<Canvas>();
                if (parentCanvas != null) canvas = parentCanvas.rootCanvas;
            }
            if (stickBase != null) baseRestPosition = stickBase.localPosition;
        }

        void OnEnable()
        {
#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Enable();
#endif
        }

        void OnDisable()
        {
            Interrupt();
#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            EnhancedTouchSupport.Disable();
#endif
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Interrupt();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) Interrupt();
        }

        /// <summary>Drop the finger and zero the output, e.g. when a menu opens over the stick.</summary>
        public void Interrupt()
        {
            mouseDown = false;
            if (tracker == null) return;
            tracker.Interrupt();
            UpdateVisuals();
        }

        void Update()
        {
            tracker.Config = config;
            tracker.Area = ScreenArea();
            tracker.UnitsPerPoint = PixelsPerPoint;
            CollectTouches(touches);
            // Unscaled time: the stick must keep responding while the game is paused with timeScale 0.
            tracker.Step(touches, Time.unscaledDeltaTime * 1000f);
            UpdateVisuals();
        }

        void CollectTouches(List<StickTouch> list)
        {
            list.Clear();
#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            foreach (InputTouch touch in InputTouch.activeTouches)
            {
                list.Add(new StickTouch(touch.touchId, touch.screenPosition, Convert(touch.phase)));
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                UnityEngine.Touch touch = UnityEngine.Input.GetTouch(i);
                list.Add(new StickTouch(touch.fingerId, touch.position, Convert(touch.phase)));
            }
#endif
            if (mouseInEditor && Application.isEditor && list.Count == 0) AddMouse(list);
        }

        void AddMouse(List<StickTouch> list)
        {
            if (!ReadMouse(out bool pressed, out Vector2 position))
            {
                mouseDown = false;
                return;
            }
            StickTouchPhase phase;
            if (pressed) phase = mouseDown ? StickTouchPhase.Moved : StickTouchPhase.Began;
            else if (mouseDown) phase = StickTouchPhase.Ended;
            else return;
            mouseDown = pressed;
            list.Add(new StickTouch(MouseTouchId, position, phase));
        }

        static bool ReadMouse(out bool pressed, out Vector2 position)
        {
#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            pressed = mouse != null && mouse.leftButton.isPressed;
            position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            return mouse != null;
#elif ENABLE_LEGACY_INPUT_MANAGER
            pressed = UnityEngine.Input.GetMouseButton(0);
            position = UnityEngine.Input.mousePosition;
            return true;
#else
            pressed = false;
            position = Vector2.zero;
            return false;
#endif
        }

#if GIL_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        static StickTouchPhase Convert(InputTouchPhase phase)
        {
            switch (phase)
            {
                case InputTouchPhase.Began: return StickTouchPhase.Began;
                case InputTouchPhase.Moved: return StickTouchPhase.Moved;
                case InputTouchPhase.Ended: return StickTouchPhase.Ended;
                case InputTouchPhase.Canceled: return StickTouchPhase.Canceled;
                default: return StickTouchPhase.Stationary;
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        static StickTouchPhase Convert(UnityEngine.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.TouchPhase.Began: return StickTouchPhase.Began;
                case UnityEngine.TouchPhase.Moved: return StickTouchPhase.Moved;
                case UnityEngine.TouchPhase.Ended: return StickTouchPhase.Ended;
                case UnityEngine.TouchPhase.Canceled: return StickTouchPhase.Canceled;
                default: return StickTouchPhase.Stationary;
            }
        }
#endif

        Camera EventCamera => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        Rect ScreenArea()
        {
            if (area == null) return new Rect(0f, 0f, Screen.width, Screen.height);
            area.GetWorldCorners(corners);
            Camera eventCamera = EventCamera;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        void UpdateVisuals()
        {
            if (tracker == null) return;
            Camera eventCamera = EventCamera;
            if (!tracker.IsPressed)
            {
                if (stickBase != null) stickBase.localPosition = baseRestPosition;
                if (knob != null) knob.anchoredPosition = Vector2.zero;
                return;
            }
            if (stickBase != null && config.floating && stickBase.parent is RectTransform baseParent &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(baseParent, tracker.Origin, eventCamera, out Vector2 basePoint))
            {
                stickBase.localPosition = new Vector3(basePoint.x, basePoint.y, baseRestPosition.z);
            }
            if (knob != null && knob.parent is RectTransform knobParent)
            {
                Vector2 offset = tracker.Point - tracker.Origin;
                float radiusPixels = config.radius * tracker.UnitsPerPoint;
                if (offset.magnitude > radiusPixels) offset = offset.normalized * radiusPixels;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(knobParent, tracker.Origin, eventCamera, out Vector2 from) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(knobParent, tracker.Origin + offset, eventCamera, out Vector2 to))
                {
                    knob.anchoredPosition = to - from;
                }
            }
        }
    }
}
