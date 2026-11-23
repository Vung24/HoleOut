using System;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Luật quà hằng ngày theo chu kỳ: mỗi ngày nhận một lần; nhận liên tiếp thì lên ngày kế, bỏ một ngày thì về ngày 1;
    /// hết chu kỳ thì vòng lại. Ngày tính bằng số ngày (theo lịch máy), trạng thái do nơi gọi lưu.
    /// </summary>
    public sealed class DailyReward
    {
        public struct State
        {
            /// <summary>Ngày đã nhận gần nhất; âm nếu chưa nhận lần nào.</summary>
            public int lastClaimDay;
            /// <summary>Ô đã nhận gần nhất trong chu kỳ (0..cycle-1); âm nếu chưa nhận.</summary>
            public int lastIndex;
        }

        readonly int _cycle;

        public DailyReward(int cycle) => _cycle = Math.Max(1, cycle);

        public static int DayNumber(DateTime local) => (int)(local.Date - new DateTime(2000, 1, 1)).TotalDays;

        public bool CanClaim(State s, int today) => s.lastClaimDay < 0 || today > s.lastClaimDay;

        /// <summary>Ô sẽ nhận hôm nay (hoặc ô vừa nhận nếu hôm nay đã nhận).</summary>
        public int TodayIndex(State s, int today)
        {
            if (s.lastClaimDay < 0 || s.lastIndex < 0) return 0;
            if (today == s.lastClaimDay) return s.lastIndex;
            if (today == s.lastClaimDay + 1) return (s.lastIndex + 1) % _cycle;
            return 0;
        }

        /// <summary>Nhận quà hôm nay; trả false nếu đã nhận.</summary>
        public bool Claim(ref State s, int today, out int index)
        {
            index = TodayIndex(s, today);
            if (!CanClaim(s, today)) return false;
            s.lastClaimDay = today;
            s.lastIndex = index;
            return true;
        }
    }
}
