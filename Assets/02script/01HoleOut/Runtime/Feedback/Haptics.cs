using UnityEngine;

namespace HoleOut.Runtime
{
    public enum HapticType { None, Light, Medium, Heavy }

    /// <summary>Rung ngắn theo mức. Android dùng VibrationEffect (API 26+) để chỉnh độ mạnh; iOS chỉ có rung mặc định nên chỉ dùng cho mức Heavy.</summary>
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk;
        static bool _ready;

        static void Init()
        {
            if (_ready) return;
            _ready = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e) { Debug.LogWarning("[HoleOut] Không khởi tạo được rung: " + e.Message); }
        }

        // Tham chiếu để Unity tự thêm quyền VIBRATE vào manifest Android.
        static void RequireVibratePermission() => Handheld.Vibrate();
#endif

        public static void Play(HapticType level)
        {
            if (level == HapticType.None) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Init();
            if (_vibrator == null) return;
            long ms = level == HapticType.Light ? 12 : level == HapticType.Medium ? 24 : 45;
            int amplitude = level == HapticType.Light ? 60 : level == HapticType.Medium ? 140 : 255;
            try
            {
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else _vibrator.Call("vibrate", ms);
            }
            catch (System.Exception) { }
#elif UNITY_IOS && !UNITY_EDITOR
            if (level == HapticType.Heavy) Handheld.Vibrate();
#endif
        }
    }
}
