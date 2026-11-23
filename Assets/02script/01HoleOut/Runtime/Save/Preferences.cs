using System;
using UnityEngine;

namespace HoleOut.Runtime
{
    public enum PreferenceKey { EntranceAnimation, Sound, Vibration }

    public static class Preferences
    {
        /// <summary>Phát khi một tùy chọn đổi giá trị.</summary>
        public static event Action<PreferenceKey> Changed;

        public static bool EntranceAnimation
        {
            get => Get(PreferenceKey.EntranceAnimation);
            set => Set(PreferenceKey.EntranceAnimation, value);
        }

        public static bool Sound
        {
            get => Get(PreferenceKey.Sound);
            set => Set(PreferenceKey.Sound, value);
        }

        public static bool Vibration
        {
            get => Get(PreferenceKey.Vibration);
            set => Set(PreferenceKey.Vibration, value);
        }

        public static bool Get(PreferenceKey p) => PlayerPrefs.GetInt(Key(p), 1) != 0;

        public static void Set(PreferenceKey p, bool value)
        {
            if (Get(p) == value) return;
            PlayerPrefs.SetInt(Key(p), value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(p);
        }

        static string Key(PreferenceKey p) => "HoleOut." + p;
    }
}
