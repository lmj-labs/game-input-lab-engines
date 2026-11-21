using UnityEngine;

namespace GameInputLab
{
    /// <summary>
    /// Plays a <see cref="HapticPattern"/>. Android runs the pattern through Vibrator and VibrationEffect, at the designed
    /// strengths when the phone can set them. Unity has no CoreHaptics, so iOS only gets <c>Handheld.Vibrate()</c>: use
    /// <see cref="HapticPattern.IosEvents"/> with a CoreHaptics plugin of your own for the real pattern. The editor does nothing.
    /// Android needs the VIBRATE permission in the manifest.
    /// </summary>
    public static class HapticPlayer
    {
        public static void Play(HapticPattern pattern)
        {
            if (pattern == null) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(pattern.Normalized());
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void Cancel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject vibrator = Vibrator())
            {
                vibrator?.Call("cancel");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject Vibrator()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                return activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
        }

        static void PlayAndroid(HapticPattern pattern)
        {
            using (AndroidJavaObject vibrator = Vibrator())
            {
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator")) return;
                long[] legacy = pattern.LegacyTimings();
                int api;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) api = version.GetStatic<int>("SDK_INT");
                if (api < 26)
                {
                    vibrator.Call("vibrate", legacy, -1);
                    return;
                }
                using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    AndroidJavaObject effect;
                    if (vibrator.Call<bool>("hasAmplitudeControl"))
                    {
                        pattern.AndroidWaveform(out long[] timings, out int[] amplitudes);
                        effect = effects.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1);
                    }
                    else
                    {
                        effect = effects.CallStatic<AndroidJavaObject>("createWaveform", legacy, -1);
                    }
                    using (effect) vibrator.Call("vibrate", effect);
                }
            }
        }
#endif
    }
}
