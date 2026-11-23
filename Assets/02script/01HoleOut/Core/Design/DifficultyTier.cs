using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    public struct TierCheck
    {
        public string metric, measured, target;
        public bool ok;
    }

    /// <summary>
    /// Ngưỡng của một cấp độ khó. Mọi ngưỡng đều là "ít nhất khó chừng này":
    /// màn đạt một cấp khi đạt mọi ngưỡng của cấp đó.
    /// </summary>
    [Serializable]
    public class DifficultyTier
    {
        public string name;
        public int suggestedDockSlots = 4;
        public int suggestedWaves = 1;
        public float maxRandomWinRate = 1f;
        public int minGreedyLosses;
        public int minWrongCapacityTraps;
        public int minWave1Traps;
        public float minBaitStepRatio;
        public int maxBreather = 999;

        public List<TierCheck> Check(DifficultyReport r)
        {
            var list = new List<TierCheck>();
            void Add(string metric, string measured, string target, bool ok) =>
                list.Add(new TierCheck { metric = metric, measured = measured, target = target, ok = ok });

            Add("Có lời giải bền", r.solvable ? (r.robust ? "có" : "có, không bền") : "không", "có", r.solvable && r.robust);
            Add("Thắng ngẫu nhiên", $"{100f * r.RandomWinRate:0.##}%", $"≤ {100f * maxRandomWinRate:0.##}%", r.RandomWinRate <= maxRandomWinRate);
            Add("Người chơi tham thua", $"{r.greedyLosses}/{r.greedy.Count}", $"≥ {minGreedyLosses}", r.greedyLosses >= minGreedyLosses);
            Add("Bẫy sai số", r.wrongCapacityTraps.ToString(), $"≥ {minWrongCapacityTraps}", r.wrongCapacityTraps >= minWrongCapacityTraps);
            Add("Bẫy sai số đợt 1", r.wrongCapacityTrapsWave1.ToString(), $"≥ {minWave1Traps}", r.wrongCapacityTrapsWave1 >= minWave1Traps);
            if (r.boardAnalyzed)
            {
                Add("Bước có mồi", $"{100f * r.BaitStepRatio:0}%", $"≥ {100f * minBaitStepRatio:0}%", r.BaitStepRatio >= minBaitStepRatio);
                Add("Khoảng thở dài nhất", r.longestBreather.ToString(), $"≤ {maxBreather}", r.longestBreather <= maxBreather);
            }
            else
            {
                Add("Bước có mồi", "chưa đo bàn", $"≥ {100f * minBaitStepRatio:0}%", minBaitStepRatio <= 0);
                Add("Khoảng thở dài nhất", "chưa đo bàn", $"≤ {maxBreather}", maxBreather >= 999);
            }
            return list;
        }

        public bool IsMet(DifficultyReport r)
        {
            foreach (var c in Check(r)) if (!c.ok) return false;
            return true;
        }

        /// <summary>Chỉ số cấp khó nhất màn đạt (danh sách xếp từ dễ tới khó); -1 nếu không đạt cấp nào.</summary>
        public static int Measure(IList<DifficultyTier> tiers, DifficultyReport r)
        {
            int best = -1;
            if (r == null || tiers == null) return best;
            for (int i = 0; i < tiers.Count; i++) if (tiers[i].IsMet(r)) best = i;
            return best;
        }

        public static List<DifficultyTier> Defaults() => new List<DifficultyTier>
        {
            new DifficultyTier { name = "Dễ", suggestedDockSlots = 5, suggestedWaves = 1 },
            new DifficultyTier
            {
                name = "Thường", suggestedDockSlots = 4, suggestedWaves = 1, maxRandomWinRate = 0.2f,
                minGreedyLosses = 1, minWrongCapacityTraps = 1, minBaitStepRatio = 0.3f, maxBreather = 6
            },
            new DifficultyTier
            {
                name = "Khó", suggestedDockSlots = 4, suggestedWaves = 2, maxRandomWinRate = 0.01f,
                minGreedyLosses = 4, minWrongCapacityTraps = 4, minWave1Traps = 2, minBaitStepRatio = 0.7f, maxBreather = 2
            },
            new DifficultyTier
            {
                name = "Rất khó", suggestedDockSlots = 4, suggestedWaves = 2, maxRandomWinRate = 0.003f,
                minGreedyLosses = 4, minWrongCapacityTraps = 6, minWave1Traps = 3, minBaitStepRatio = 0.85f, maxBreather = 2
            },
        };
    }
}
