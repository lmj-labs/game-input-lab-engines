using System;
using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// Stretches its RectTransform over <c>Screen.safeArea</c>. Put it on an empty child of a full-screen
    /// Canvas and use it as the parent of the controls placed by <see cref="LandscapeLayout.Apply"/>.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        /// <summary>Raised after the panel has been resized, for example on rotation. Place controls again.</summary>
        public event Action Changed;

        Rect lastSafe;
        Vector2Int lastScreen;

        void OnEnable() => Refresh(true);

        void Update() => Refresh(false);

        void Refresh(bool force)
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;
            if (!force && safe == lastSafe && screen == lastScreen) return;
            lastSafe = safe;
            lastScreen = screen;

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Changed?.Invoke();
        }
    }
}
