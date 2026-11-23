using System;
using System.Collections.Generic;
using System.Text;

namespace HoleOut.Core
{
    public enum GreedyPolicy
    {
        /// <summary>Màu đang lộ nhiều nhất, khối số to trước.</summary>
        MostExposedLargest,
        /// <summary>Màu đang lộ nhiều nhất, khối số nhỏ trước.</summary>
        MostExposedSmallest,
        /// <summary>Khối số to nhất trong các màu đang lộ.</summary>
        LargestExposed,
        /// <summary>Khối số nhỏ nhất trong các màu đang lộ.</summary>
        SmallestExposed
    }

    [Serializable]
    public class GreedyResult
    {
        public GreedyPolicy policy;
        public bool won;
        /// <summary>Số khối đã đưa ra khi thua (bằng tổng số khối nếu thắng).</summary>
        public int launched;
    }

    /// <summary>Một bước của thứ tự tham chiếu.</summary>
    [Serializable]
    public class StepReport
    {
        public int blockId;
        public int wave;
        public string color;
        public int capacity;
        /// <summary>Số khối cùng màu, cùng đợt, khác khối đúng mà lấy thay thì hết đường thắng.</summary>
        public int wrongCapacityTraps;
        /// <summary>Số lựa chọn khác (mọi màu) mà lấy là hết đường thắng.</summary>
        public int losingChoices;
        public int choices;
        /// <summary>Bàn: số khối khác ra được cổng ngay.</summary>
        public int directBlocks;
        /// <summary>Bàn: trong số đó, khối lấy là thua (mồi).</summary>
        public int baitBlocks;
        /// <summary>Bàn: mồi cùng màu khác số với khối đúng.</summary>
        public int wrongCapacityBait;
        /// <summary>Bàn: số lần dời ít nhất để khối đúng ra cổng; -1 nếu không ra được.</summary>
        public int unlockMoves = -1;
        /// <summary>Chỗ khay còn trống sau khi lắng.</summary>
        public int freeSlotsAfter;

        public bool HasPressure(bool boardAnalyzed) => wrongCapacityTraps > 0 || (boardAnalyzed ? baitBlocks > 0 : losingChoices > 0);
    }

    /// <summary>Các thước đo độ khó của một màn. Thuần dữ liệu, lưu kèm asset màn.</summary>
    [Serializable]
    public class DifficultyReport
    {
        public bool solvable;
        /// <summary>Thứ tự tham chiếu thắng với cả 4 kiểu duyệt ô.</summary>
        public bool robust;
        /// <summary>Tìm kiếm dừng vì giới hạn: các kết luận "không có" chưa chắc chắn.</summary>
        public bool searchExhausted;
        /// <summary>"solution" (lời giải trong asset) hoặc "search" (máy tìm).</summary>
        public string referenceSource;
        public List<int> referenceOrder = new List<int>();

        public int randomSamples;
        public int randomWins;
        public float RandomWinRate => randomSamples > 0 ? (float)randomWins / randomSamples : 0;

        public List<GreedyResult> greedy = new List<GreedyResult>();
        public int greedyLosses;

        public int wrongCapacityTraps;
        public int wrongCapacityTrapsWave1;

        public bool boardAnalyzed;
        public string boardError;
        public int baitSteps;
        public int totalUnlockMoves;
        public int longestBreather;

        public List<StepReport> steps = new List<StepReport>();
        public List<string> notes = new List<string>();

        public float BaitStepRatio => steps.Count > 0 ? (float)baitSteps / steps.Count : 0;

        public string Summary()
        {
            var sb = new StringBuilder();
            if (!solvable)
            {
                sb.Append(searchExhausted ? "Chưa tìm thấy lời giải (hết giới hạn tìm kiếm)." : "Không có lời giải.");
                foreach (var n in notes) sb.Append("\n  - ").Append(n);
                return sb.ToString();
            }
            sb.Append($"Lời giải ({referenceSource}{(robust ? ", bền" : ", KHÔNG bền")}): {steps.Count} khối");
            sb.Append($"\nThắng ngẫu nhiên {100f * RandomWinRate:0.00}% ({randomWins}/{randomSamples})");
            sb.Append($" | người chơi tham thua {greedyLosses}/{greedy.Count}");
            sb.Append($" | bẫy sai số {wrongCapacityTraps} (đợt 1: {wrongCapacityTrapsWave1})");
            if (boardAnalyzed)
                sb.Append($"\nBước có mồi {100f * BaitStepRatio:0}% | tổng lần dời {totalUnlockMoves} | khoảng thở dài nhất {longestBreather}");
            else if (!string.IsNullOrEmpty(boardError))
                sb.Append($"\nBàn: {boardError}");
            sb.Append("\nTừng bước (bẫy sai số/mồi/dời):");
            int wave = -1;
            foreach (var s in steps)
            {
                if (s.wave != wave) { if (wave >= 0) sb.Append(" |"); wave = s.wave; }
                sb.Append($" {s.color}{s.capacity}:{s.wrongCapacityTraps}/{(boardAnalyzed ? s.baitBlocks.ToString() : "-")}/{(boardAnalyzed ? s.unlockMoves.ToString() : "-")}");
            }
            foreach (var n in notes) sb.Append("\n  - ").Append(n);
            return sb.ToString();
        }
    }
}
