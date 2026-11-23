using System;
using UnityEngine;

namespace HoleOut.Runtime
{
    public enum BoosterType { Lift, ExtraSlot, Magnet }

    /// <summary>Cấu hình một booster: màn mở khóa (tính từ 1) và số lượt tặng lần đầu.</summary>
    [Serializable]
    public class BoosterSettings
    {
        public BoosterType type;
        [Min(1)] public int unlockLevel = 1;
        [Min(0)] public int startCount = 3;

        public BoosterSettings() { }
        public BoosterSettings(BoosterType type, int unlockLevel, int startCount)
        {
            this.type = type;
            this.unlockLevel = unlockLevel;
            this.startCount = startCount;
        }
    }

    /// <summary>Số lượt booster người chơi đang có, lưu bền giữa các lần chơi.</summary>
    public static class BoosterInventory
    {
        static string Key(BoosterType type) => "HoleOut.Booster." + type;
        static string AnnouncedKey(BoosterType type) => "HoleOut.BoosterAnnounced." + type;

        /// <summary>Đã giới thiệu booster này cho người chơi (bảng mở khóa chỉ hiện một lần).</summary>
        public static bool Announced(BoosterType type) => PlayerPrefs.GetInt(AnnouncedKey(type), 0) != 0;

        public static void MarkAnnounced(BoosterType type)
        {
            PlayerPrefs.SetInt(AnnouncedKey(type), 1);
            PlayerPrefs.Save();
        }

        public static int Count(BoosterSettings s) => PlayerPrefs.GetInt(Key(s.type), s.startCount);

        public static bool TryConsume(BoosterSettings s)
        {
            int count = Count(s);
            if (count <= 0) return false;
            Set(s.type, count - 1);
            return true;
        }

        public static void Set(BoosterType type, int count)
        {
            PlayerPrefs.SetInt(Key(type), Mathf.Max(0, count));
            PlayerPrefs.Save();
        }
    }
}
