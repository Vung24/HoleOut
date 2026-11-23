using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HoleOut.Core;

namespace HoleOut.EditorTools
{
    /// <summary>Thông số dựng màn mặc định cho một cấp độ khó.</summary>
    public sealed class LevelDesignPreset
    {
        public string Tier;
        public int Dock;
        public int Waves;
        public double Seconds;
        public int MinPerWave = 6, MaxPerWave = 11;
        /// <summary>Số lần dựng thử (mỗi lần một seed); giữ bản khó nhất, dừng sớm khi đạt cấp.</summary>
        public int Attempts = 1;
        public string Hint;
    }

    /// <summary>
    /// Dựng trọn một màn từ tranh: chia số khối → xếp bàn và cổng → dựng lời giải → phát lại kiểm chứng.
    /// Dùng chung cho Level Editor (tab Bắt đầu nhanh) và công cụ dựng hàng loạt từ file tranh. Chạy được ở luồng nền.
    /// </summary>
    public static class LevelDesign
    {
        public const int MaxSide = DefaultBoardLayout.FrameColumns;
        public const int MaxColors = 8;
        /// <summary>Số seed thử thêm khi một lần dựng không ra lời giải.</summary>
        public const int SeedAttempts = 3;

        public static readonly LevelDesignPreset[] Presets =
        {
            new LevelDesignPreset { Tier = "Dễ", Dock = 5, Waves = 1, Seconds = 40, MinPerWave = 6, MaxPerWave = 9, Attempts = 1,
                Hint = "Khay 5 chỗ, một đợt khối. Hợp màn dạy chơi và màn nghỉ sau màn khó." },
            new LevelDesignPreset { Tier = "Thường", Dock = 4, Waves = 1, Seconds = 60, MinPerWave = 7, MaxPerWave = 11, Attempts = 1,
                Hint = "Khay 4 chỗ, một đợt khối. Cần để ý thứ tự nhưng chưa có bẫy nặng." },
            new LevelDesignPreset { Tier = "Khó", Dock = 4, Waves = 2, Seconds = 120, MinPerWave = 8, MaxPerWave = 12, Attempts = 2,
                Hint = "Khay 4 chỗ, hai đợt, nhiều khối cùng màu khác số: chọn sai số là kẹt, chơi tham luôn thua." },
            new LevelDesignPreset { Tier = "Rất khó", Dock = 4, Waves = 2, Seconds = 150, MinPerWave = 9, MaxPerWave = 13, Attempts = 3,
                Hint = "Như Khó nhưng nhiều bẫy hơn, hầu như mọi bước đều có khối mồi. Dùng cho màn chốt mỗi 10 màn. Có thể mất 10–25 phút." },
        };

        public static LevelDesignPreset Preset(string tier) => Presets.FirstOrDefault(p => p.Tier == tier) ?? Presets[1];

        static readonly (int seed, double dt)[] Checks = { (1, 1 / 30.0), (2, 1 / 60.0), (9, 1 / 20.0) };

        public sealed class Result
        {
            public LevelData Data;
            public LevelSolution Solution;
            public DifficultyReport Report;
            public int Seed;
            /// <summary>Cấp đo được theo bộ ngưỡng (rỗng nếu không đạt cấp nào).</summary>
            public string MeasuredTier;
            public bool TierMet;
            /// <summary>Tóm tắt dễ đọc cho người dùng.</summary>
            public string Message;
            public bool Ok => Data != null && Solution != null;
        }

        /// <summary>Lỗi tranh khiến không dựng được màn; danh sách rỗng là hợp lệ.</summary>
        public static List<string> PictureProblems(string[] grid)
        {
            var list = new List<string>();
            if (grid == null || grid.Length == 0) { list.Add("Tranh đang trống."); return list; }
            int cols = grid[0].Length;
            if (grid.Any(r => r.Length != cols)) list.Add("Các hàng tranh không cùng độ dài.");
            if (cols > MaxSide || grid.Length > MaxSide) list.Add($"Tranh {cols}×{grid.Length} lớn hơn khung {MaxSide}×{MaxSide}. Thu nhỏ ở tab Tranh.");
            int colors = grid.SelectMany(r => r).Distinct().Count();
            if (colors < 2) list.Add("Tranh cần ít nhất 2 màu.");
            if (colors > MaxColors) list.Add($"Tranh có {colors} màu; tối đa {MaxColors} màu để bàn đủ chỗ cho cổng. Gộp bớt màu ít ô.");
            return list;
        }

        /// <summary>
        /// Dựng màn theo cấp mục tiêu: dựng nhiều lần (mỗi lần seed khác), đo độ khó đầy đủ, giữ bản khó nhất
        /// và dừng sớm khi đạt ngưỡng cấp trong <paramref name="tiers"/>.
        /// </summary>
        public static Result Run(string id, string[] grid, string targetTier, IList<DifficultyTier> tiers, int seed, CancellationToken token,
            int dock = 0, int waves = 0, double seconds = 0)
        {
            var preset = Preset(targetTier);
            dock = dock > 0 ? dock : preset.Dock;
            waves = waves > 0 ? waves : preset.Waves;
            seconds = seconds > 0 ? seconds : preset.Seconds;
            tiers = tiers ?? DifficultyTier.Defaults();
            int target = Math.Max(0, IndexOf(tiers, preset.Tier));

            var problems = PictureProblems(grid);
            if (problems.Count > 0) return new Result { Message = string.Join(" ", problems) };
            Result best = null, lastFail = null;
            int bestRank = -2, tries = 0, next = seed;
            while (tries < preset.Attempts || (best == null && tries < preset.Attempts + SeedAttempts))
            {
                token.ThrowIfCancellationRequested();
                tries++;
                var r = RunOnce(id, grid, dock, waves, seconds, next++, token, preset.MinPerWave, preset.MaxPerWave);
                if (!r.Ok) { lastFail = r; continue; }
                r.Report = DifficultyAnalyzer.Analyze(r.Data, r.Solution, new DifficultyOptions { Cancel = token });
                int rank = DifficultyTier.Measure(tiers, r.Report);
                if (best == null || rank > bestRank || (rank == bestRank && r.Report.RandomWinRate < best.Report.RandomWinRate))
                {
                    best = r;
                    bestRank = rank;
                }
                if (bestRank >= target) break;
            }
            if (best == null) return lastFail;
            best.MeasuredTier = bestRank >= 0 ? tiers[bestRank].name : "";
            best.TierMet = bestRank >= target;
            string tierText = best.TierMet ? $"Đạt cấp {preset.Tier} (đo được: {best.MeasuredTier})."
                : $"Chưa đạt cấp {preset.Tier} sau {tries} lần thử; giữ bản khó nhất (đo được: {(best.MeasuredTier == "" ? "dưới Dễ" : best.MeasuredTier)}). Bấm \"Thử cách khác\" hoặc dùng tranh nhiều màu hơn.";
            best.Message = tierText + " " + Summary(best);
            return best;
        }

        static int IndexOf(IList<DifficultyTier> tiers, string name)
        {
            for (int i = 0; i < tiers.Count; i++) if (tiers[i].name == name) return i;
            return -1;
        }

        static string Summary(Result r)
        {
            int blocks = r.Data.waves.Sum(w => w.blocks.Count);
            var rep = r.Report;
            return $"{blocks} khối, {r.Solution.steps.Count} bước, chơi lại 3 lần đều thắng. Chọn bừa thắng {100 * rep.RandomWinRate:0.#}% · " +
                   $"bẫy sai số {rep.wrongCapacityTraps} · chơi tham thua {rep.greedyLosses}/{rep.greedy.Count}.";
        }

        static Result RunOnce(string id, string[] grid, int dock, int waves, double seconds, int seed, CancellationToken token, int minPerWave, int maxPerWave)
        {
            var result = new Result { Seed = seed };
            var picture = new LevelData
            {
                levelId = id, grid = grid, displayMode = PictureDisplayMode.FullPicture,
                visibleRows = grid.Length, pictureScale = 1, dockSlots = dock
            };
            var caps = CapsDesigner.Run(picture, null, new CapsDesignOptions
            {
                Seconds = seconds, Seed = seed, Waves = waves, MinPerWave = minPerWave,
                MaxPerWave = Math.Max(minPerWave, maxPerWave), Cancel = token
            });
            if (caps == null) { result.Message = "Không tìm được cách chia số khối có lời giải chắc chắn. Thử độ khó thấp hơn hoặc tranh ít màu hơn."; return result; }
            var level = CapsDesigner.NumbersLevel(picture, caps.Blocks, waves);
            var order = caps.Order.ToList();

            var board = BoardDesigner.Run(level, order, new BoardDesignOptions { Seconds = seconds, Seed = seed, Cancel = token });
            if (board == null) { result.Message = "Không xếp được bàn để mọi khối ra được cổng. Thử lại hoặc giảm độ khó."; return result; }
            level.waves = board.Waves;
            level.gates = board.Gates;

            var built = SolutionBuilder.Build(level, order);
            if (!built.Success) { result.Message = "Không dựng được lời giải: " + built.Error; return result; }
            var failed = new List<string>();
            foreach (var (s, dt) in Checks)
            {
                token.ThrowIfCancellationRequested();
                var r = SolutionReplayer.Run(level, built.Solution, dt, s);
                if (!r.Success) failed.Add($"seed {s}, dt 1/{1 / dt:0}: {r}");
            }
            if (failed.Count > 0) { result.Message = "Lời giải không chạy lại được: " + string.Join("; ", failed); return result; }

            result.Data = level;
            result.Solution = built.Solution;
            result.Report = caps.Report;
            result.Message = Summary(result);
            return result;
        }
    }
}
