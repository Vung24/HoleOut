using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Cấu hình kinh tế: coin thưởng theo cấp độ khó của màn, hệ số nhân đôi và giá booster.</summary>
    [CreateAssetMenu(menuName = "HoleOut/Economy", fileName = "HoleOutEconomy")]
    public class EconomySO : ScriptableObject
    {
        [Serializable]
        public struct TierReward
        {
            [Tooltip("Tên cấp, trùng DifficultyTier (Dễ, Thường, Khó, Rất khó).")]
            public string tier;
            [Min(0)] public int coins;
        }

        [Serializable]
        public struct BoosterPrice
        {
            public BoosterType booster;
            [Min(0)] public int coins;
        }

        [Min(0)] public int defaultWinCoins = 10;
        public List<TierReward> winCoins = new List<TierReward>
        {
            new TierReward { tier = "Dễ", coins = 10 },
            new TierReward { tier = "Thường", coins = 15 },
            new TierReward { tier = "Khó", coins = 25 },
            new TierReward { tier = "Rất khó", coins = 40 },
        };
        [Min(1)] public int doubleMultiplier = 2;
        [Header("Chuỗi thắng và album")]
        [Min(0), Tooltip("Coin thêm cho mỗi bậc chuỗi thắng (từ trận thắng thứ 2 liên tiếp).")]
        public int streakStep = 5;
        [Min(0)] public int streakMax = 25;
        [Min(1), Tooltip("Số màn mỗi theme/album.")]
        public int themeSize = 10;
        [Min(0), Tooltip("Coin thưởng khi hoàn thành trọn một album.")]
        public int themeBonus = 200;

        /// <summary>Coin thêm theo chuỗi: streak là số trận thắng liên tiếp tính cả trận này.</summary>
        public int StreakBonus(int streak) => Mathf.Min(streakMax, Mathf.Max(0, streak - 1) * streakStep);

        /// <summary>Thưởng album khi thắng màn cuối của một nhóm themeSize màn (levelNumber tính từ 1).</summary>
        public int ThemeBonus(int levelNumber) => levelNumber > 0 && levelNumber % themeSize == 0 ? themeBonus : 0;

        [Tooltip("Coin quà hằng ngày theo từng ngày trong chu kỳ (ngày 1 → ngày cuối).")]
        public int[] dailyCoins = { 20, 30, 40, 50, 60, 80, 150 };
        public List<BoosterPrice> boosterPrices = new List<BoosterPrice>
        {
            new BoosterPrice { booster = BoosterType.Lift, coins = 100 },
            new BoosterPrice { booster = BoosterType.ExtraSlot, coins = 120 },
            new BoosterPrice { booster = BoosterType.Magnet, coins = 150 },
        };

        public int WinCoins(string tier)
        {
            foreach (var r in winCoins) if (r.tier == tier) return r.coins;
            return defaultWinCoins;
        }

        public int Price(BoosterType booster)
        {
            foreach (var p in boosterPrices) if (p.booster == booster) return p.coins;
            return 0;
        }
    }
}
