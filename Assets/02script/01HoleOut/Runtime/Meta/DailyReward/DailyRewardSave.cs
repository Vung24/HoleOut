using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Lưu trạng thái quà hằng ngày bằng PlayerPrefs.</summary>
    public static class DailyRewardSave
    {
        const string DayKey = "HoleOut.Daily.LastDay", IndexKey = "HoleOut.Daily.LastIndex";

        public static DailyReward.State Load() => new DailyReward.State
        {
            lastClaimDay = PlayerPrefs.GetInt(DayKey, -1),
            lastIndex = PlayerPrefs.GetInt(IndexKey, -1)
        };

        public static void Save(DailyReward.State s)
        {
            PlayerPrefs.SetInt(DayKey, s.lastClaimDay);
            PlayerPrefs.SetInt(IndexKey, s.lastIndex);
            PlayerPrefs.Save();
        }
    }
}
