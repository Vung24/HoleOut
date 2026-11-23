using HoleOut.Runtime;
using NUnit.Framework;

namespace HoleOut.Tests
{
    public class DailyRewardTests
    {
        static DailyReward.State Fresh => new DailyReward.State { lastClaimDay = -1, lastIndex = -1 };

        [Test]
        public void FirstClaim_IsDayOne_OncePerDay()
        {
            var d = new DailyReward(7);
            var s = Fresh;
            Assert.IsTrue(d.Claim(ref s, 100, out int i));
            Assert.AreEqual(0, i);
            Assert.IsFalse(d.Claim(ref s, 100, out _));
            Assert.IsFalse(d.CanClaim(s, 100));
        }

        [Test]
        public void ConsecutiveDays_Advance_AndWrap()
        {
            var d = new DailyReward(3);
            var s = Fresh;
            for (int day = 0; day < 4; day++) d.Claim(ref s, 50 + day, out _);
            Assert.AreEqual(0, s.lastIndex, "Ngày thứ 4 của chu kỳ 3 vòng về ô đầu.");
        }

        [Test]
        public void MissedDay_ResetsToDayOne()
        {
            var d = new DailyReward(7);
            var s = Fresh;
            d.Claim(ref s, 10, out _);
            d.Claim(ref s, 11, out _);
            Assert.AreEqual(1, s.lastIndex);
            Assert.AreEqual(0, d.TodayIndex(s, 13));
            d.Claim(ref s, 13, out int i);
            Assert.AreEqual(0, i);
        }
    }
}
