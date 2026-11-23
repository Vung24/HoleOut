using HoleOut.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace HoleOut.Tests
{
    public class EconomyTests
    {
        sealed class FakeWallet : IWallet
        {
            public int Balance { get; private set; }
            public void Add(int amount) => Balance += amount;
            public bool TrySpend(int amount)
            {
                if (Balance < amount) return false;
                Balance -= amount;
                return true;
            }
        }

        [Test]
        public void WinReward_ClaimsOnce()
        {
            var wallet = new FakeWallet();
            var reward = new WinReward(wallet);
            reward.Prepare(15, 2);
            Assert.AreEqual(15, reward.Claim(false));
            Assert.AreEqual(0, reward.Claim(true));
            Assert.AreEqual(15, wallet.Balance);
        }

        [Test]
        public void WinReward_DoubleUsesMultiplier()
        {
            var wallet = new FakeWallet();
            var reward = new WinReward(wallet);
            reward.Prepare(25, 2);
            Assert.AreEqual(50, reward.DoubledCoins);
            Assert.AreEqual(50, reward.Claim(true));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void WinReward_NotReadyUntilPreparedAndResetsPerLevel()
        {
            var wallet = new FakeWallet();
            var reward = new WinReward(wallet);
            Assert.IsFalse(reward.CanClaim);
            Assert.AreEqual(0, reward.Claim(false));
            reward.Prepare(10, 2);
            reward.Prepare(99, 2);
            Assert.AreEqual(10, reward.Coins, "Chốt lần đầu, gọi lại không đổi.");
            reward.Claim(false);
            reward.Reset();
            reward.Prepare(40, 2);
            Assert.AreEqual(40, reward.Claim(false));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void Economy_StreakAndThemeBonus()
        {
            var economy = ScriptableObject.CreateInstance<EconomySO>();
            Assert.AreEqual(0, economy.StreakBonus(1), "Trận thắng đầu chưa có chuỗi.");
            Assert.AreEqual(economy.streakStep, economy.StreakBonus(2));
            Assert.AreEqual(economy.streakMax, economy.StreakBonus(100));
            Assert.AreEqual(economy.themeBonus, economy.ThemeBonus(economy.themeSize));
            Assert.AreEqual(0, economy.ThemeBonus(economy.themeSize + 1));
            Object.DestroyImmediate(economy);
        }

        [Test]
        public void Economy_TierAndPriceLookup()
        {
            var economy = ScriptableObject.CreateInstance<EconomySO>();
            Assert.AreEqual(25, economy.WinCoins("Khó"));
            Assert.AreEqual(economy.defaultWinCoins, economy.WinCoins(null));
            Assert.Greater(economy.Price(BoosterType.Magnet), 0);
            Object.DestroyImmediate(economy);
        }
    }
}
