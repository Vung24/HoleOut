using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Tiến trình chiến dịch: chỉ số màn (từ 0) người chơi sẽ vào khi bấm Chơi.</summary>
    public static class PlayerProgress
    {
        const string LevelKey = "HoleOut.Progress.Level";

        const string StreakKey = "HoleOut.Progress.Streak";
        const string LevelIdKey = "HoleOut.Progress.LevelId";

        /// <summary>Lưu màn sẽ vào: cả mã màn (bền khi đổi thứ tự chiến dịch) lẫn chỉ số dự phòng.</summary>
        public static void SaveNext(int index, string levelId)
        {
            PlayerPrefs.SetString(LevelIdKey, levelId ?? "");
            LevelIndex = index;
        }

        /// <summary>Chỉ số màn sẽ vào trong <paramref name="ids"/>: tìm theo mã màn đã lưu, không thấy thì dùng chỉ số (vòng lại).</summary>
        public static int ResolveIndex(System.Collections.Generic.IList<string> ids)
        {
            if (ids == null || ids.Count == 0) return 0;
            string id = PlayerPrefs.GetString(LevelIdKey, "");
            if (id.Length > 0)
                for (int i = 0; i < ids.Count; i++)
                    if (ids[i] == id) return i;
            return LevelIndex % ids.Count;
        }

        public static bool HasSave => PlayerPrefs.HasKey(LevelKey);

        /// <summary>Số trận thắng liên tiếp chưa bị kẹt.</summary>
        public static int WinStreak
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(StreakKey, 0));
            set
            {
                PlayerPrefs.SetInt(StreakKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static int LevelIndex
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(LevelKey, 0));
            set
            {
                PlayerPrefs.SetInt(LevelKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }
    }
}
