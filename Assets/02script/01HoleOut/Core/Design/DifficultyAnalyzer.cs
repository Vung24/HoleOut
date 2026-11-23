using System;
using System.Collections.Generic;
using System.Threading;

namespace HoleOut.Core
{
    public sealed class DifficultyOptions
    {
        public int RandomSamples = 300;
        public uint RandomSeed = 12345;
        /// <summary>Số nút tối đa khi máy tìm lời giải bền.</summary>
        public int SearchNodeLimit = 400000;
        /// <summary>Ngân sách mỗi câu hỏi "lấy khối này còn thắng được không".</summary>
        public int OracleBudget = 60000;
        /// <summary>Số trạng thái bàn tối đa khi tìm số lần dời.</summary>
        public int BoardStateLimit = 15000;
        /// <summary>Kiểm tra dữ liệu bằng validator trước khi đo. Tắt khi chỉ đo số (vị trí khối chưa có).</summary>
        public bool Validate = true;
        /// <summary>Đo mồi và số lần dời trên bàn. Tắt khi chỉ cần đo số và đợt.</summary>
        public bool AnalyzeBoard = true;
        public CancellationToken Cancel;
    }

    /// <summary>
    /// Đo độ khó một màn không cần mechanic mới: tỉ lệ thắng ngẫu nhiên, người chơi tham,
    /// bẫy sai số, mồi sát cổng và số lần dời. Luật tranh/khay lấy từ <see cref="DrainModel"/>,
    /// luật bàn từ <see cref="DragSolver"/>.
    /// </summary>
    public static class DifficultyAnalyzer
    {
        public static DifficultyReport Analyze(LevelData level, LevelSolution solution = null, DifficultyOptions options = null)
        {
            options = options ?? new DifficultyOptions();
            var report = new DifficultyReport();

            var errors = options.Validate ? LevelValidator.Validate(level) : new List<string>();
            if (errors.Count > 0)
            {
                report.notes.Add("Dữ liệu màn chưa hợp lệ:");
                report.notes.AddRange(errors);
                return report;
            }

            var models = OrderSearch.CreateModels(level);
            var model = models[0];
            var oracle = new WinOracle(model, options.Cancel);

            MeasureRandom(model, options, report);
            MeasureGreedy(model, report);

            var order = PickReference(level, solution, models, oracle, options, report);
            if (order == null) return report;

            report.solvable = true;
            report.referenceOrder = order;
            MeasureSteps(model, oracle, order, options, report);
            if (options.AnalyzeBoard) MeasureBoard(level, model, oracle, order, options, report);
            Aggregate(report);
            return report;
        }

        // ───────────── Thứ tự tham chiếu ─────────────

        static List<int> PickReference(LevelData level, LevelSolution solution, DrainModel[] models,
            WinOracle oracle, DifficultyOptions options, DifficultyReport report)
        {
            var model = models[0];
            List<int> fromSolution = null;
            if (solution != null && solution.HasSteps)
            {
                fromSolution = new List<int>();
                foreach (var st in solution.steps)
                {
                    if (st.kind != SolutionStepKind.Exit) continue;
                    int idx = model.IndexOfBlockId(st.block);
                    if (idx < 0) { report.notes.Add($"Lời giải nhắc khối {st.block} không có trong màn."); fromSolution = null; break; }
                    fromSolution.Add(idx);
                }
            }

            if (fromSolution != null)
            {
                var end = model.Play(fromSolution);
                if (end.Status == DrainStatus.Won)
                {
                    if (OrderSearch.IsRobust(level, fromSolution, models))
                    {
                        report.referenceSource = "solution";
                        report.robust = true;
                        return fromSolution;
                    }
                    report.notes.Add("Lời giải hiện có không bền: thua với ít nhất một kiểu duyệt ô.");
                }
                else
                    report.notes.Add($"Lời giải hiện có không thắng trong mô hình nhanh (dừng sau {end.LaunchCount} khối, còn {end.Remaining} ô). " +
                                     "Nếu replay trong game vẫn thắng thì mô hình đang lệch luật.");
            }

            var found = OrderSearch.FindRobust(oracle, models, options.SearchNodeLimit, options.Cancel);
            if (found.Order != null)
            {
                report.referenceSource = "search";
                report.robust = true;
                return found.Order;
            }

            if (fromSolution != null && model.Play(fromSolution).Status == DrainStatus.Won)
            {
                report.referenceSource = "solution";
                report.robust = false;
                return fromSolution;
            }

            report.searchExhausted = found.Exhausted;
            report.notes.Add(found.Exhausted
                ? $"Đã duyệt {found.Nodes} nút mà chưa thấy lời giải bền."
                : "Không có thứ tự đưa khối nào thắng với cả 4 kiểu duyệt ô.");
            return null;
        }

        // ───────────── Ngẫu nhiên và người chơi tham ─────────────

        static void MeasureRandom(DrainModel model, DifficultyOptions options, DifficultyReport report)
        {
            uint rng = options.RandomSeed;
            var choices = new List<int>();
            int wins = 0;
            for (int t = 0; t < options.RandomSamples; t++)
            {
                var s = model.Initial();
                while (s.Status == DrainStatus.Playing)
                {
                    model.Choices(s, choices);
                    if (choices.Count == 0) break;
                    rng = rng * 1664525u + 1013904223u;
                    model.LaunchInPlace(s, choices[(int)(rng / 4294967296.0 * choices.Count)]);
                }
                if (s.Status == DrainStatus.Won) wins++;
            }
            report.randomSamples = options.RandomSamples;
            report.randomWins = wins;
        }

        static void MeasureGreedy(DrainModel model, DifficultyReport report)
        {
            var exposed = new Dictionary<string, int>();
            var choices = new List<int>();
            foreach (GreedyPolicy policy in Enum.GetValues(typeof(GreedyPolicy)))
            {
                var s = model.Initial();
                while (s.Status == DrainStatus.Playing)
                {
                    model.Choices(s, choices);
                    if (choices.Count == 0) break;
                    model.CountExposed(s, exposed);
                    model.LaunchInPlace(s, Pick(model, policy, choices, exposed));
                }
                bool won = s.Status == DrainStatus.Won;
                report.greedy.Add(new GreedyResult { policy = policy, won = won, launched = s.LaunchCount });
                if (!won) report.greedyLosses++;
            }
        }

        static int Pick(DrainModel model, GreedyPolicy policy, List<int> choices, Dictionary<string, int> exposed)
        {
            int best = choices[0];
            for (int i = 1; i < choices.Count; i++)
                if (Better(model, policy, choices[i], best, exposed)) best = choices[i];
            return best;
        }

        /// <summary>a tốt hơn b theo quy tắc; hòa thì giữ khối đứng trước.</summary>
        static bool Better(DrainModel model, GreedyPolicy policy, int a, int b, Dictionary<string, int> exposed)
        {
            BlockData A = model.Blocks[a], B = model.Blocks[b];
            exposed.TryGetValue(A.color, out int ea);
            exposed.TryGetValue(B.color, out int eb);
            switch (policy)
            {
                case GreedyPolicy.MostExposedLargest:
                    return ea != eb ? ea > eb : A.capacity > B.capacity;
                case GreedyPolicy.MostExposedSmallest:
                    return ea != eb ? ea > eb : A.capacity < B.capacity;
                case GreedyPolicy.LargestExposed:
                    return (ea > 0) != (eb > 0) ? ea > 0 : A.capacity > B.capacity;
                default:
                    return (ea > 0) != (eb > 0) ? ea > 0 : A.capacity < B.capacity;
            }
        }

        // ───────────── Bẫy theo từng bước ─────────────

        static void MeasureSteps(DrainModel model, WinOracle oracle, List<int> order, DifficultyOptions options, DifficultyReport report)
        {
            var s = model.Initial();
            var choices = new List<int>();
            foreach (int id in order)
            {
                var correct = model.Blocks[id];
                var step = new StepReport
                {
                    blockId = correct.id, wave = model.BlockWave[id], color = correct.color, capacity = correct.capacity
                };
                model.Choices(s, choices);
                step.choices = choices.Count;
                foreach (int j in choices)
                {
                    if (j == id) continue;
                    var v = oracle.CanWin(model.Launch(s, j), options.OracleBudget);
                    if (v != WinVerdict.Lose) continue;
                    step.losingChoices++;
                    var other = model.Blocks[j];
                    if (ReferenceEquals(other.color, correct.color) && other.capacity != correct.capacity) step.wrongCapacityTraps++;
                }
                model.LaunchInPlace(s, id);
                step.freeSlotsAfter = model.Slots - s.Dock.Count;
                report.steps.Add(step);
            }
        }

        // ───────────── Bàn: mồi và số lần dời ─────────────

        static void MeasureBoard(LevelData level, DrainModel model, WinOracle oracle, List<int> order, DifficultyOptions options, DifficultyReport report)
        {
            bool hasMystery = false;
            foreach (var b in model.Blocks) hasMystery |= b.mystery;
            if (hasMystery) report.notes.Add("Có khối bí ẩn: phần bàn coi mọi khối đã lộ màu.");

            var s = model.Initial();
            var choices = new List<int>();
            int wave = -1;
            var board = new List<BlockData>();

            for (int k = 0; k < order.Count; k++)
            {
                options.Cancel.ThrowIfCancellationRequested();
                int id = order[k];
                if (board.Count == 0)
                {
                    wave++;
                    if (wave >= level.waves.Count) { report.boardError = "Thứ tự tham chiếu dài hơn số đợt."; return; }
                    foreach (var b in level.waves[wave].blocks) { var c = b.Copy(); c.mystery = false; board.Add(c); }
                }

                var targetData = model.Blocks[id];
                var target = board.Find(b => b.id == targetData.id);
                if (target == null) { report.boardError = $"Bước {k + 1}: khối {targetData.id} không có trên bàn đợt {wave + 1}."; return; }

                model.Choices(s, choices);
                var step = report.steps[k];
                foreach (var b in board)
                {
                    if (b == target || !BoardReach.CanExit(b, board, level.gates)) continue;
                    step.directBlocks++;
                    int j = model.IndexOfBlockId(b.id);
                    if (!choices.Contains(j)) continue;
                    if (oracle.CanWin(model.Launch(s, j), options.OracleBudget) != WinVerdict.Lose) continue;
                    step.baitBlocks++;
                    if (b.color == target.color && b.capacity != target.capacity) step.wrongCapacityBait++;
                }

                var moves = BoardReach.Unlock(target, board, level.gates, options.BoardStateLimit);
                if (moves == null)
                {
                    report.boardError = $"Bước {k + 1}: khối {target.id} ({target.color}{target.capacity}) không ra được cổng trong giới hạn {options.BoardStateLimit} trạng thái.";
                    return;
                }
                step.unlockMoves = moves.Count;
                board.Remove(target);
                model.LaunchInPlace(s, id);
            }
            report.boardAnalyzed = true;
        }

        static void Aggregate(DifficultyReport report)
        {
            int run = 0;
            foreach (var st in report.steps)
            {
                report.wrongCapacityTraps += st.wrongCapacityTraps;
                if (st.wave == 0) report.wrongCapacityTrapsWave1 += st.wrongCapacityTraps;
                if (report.boardAnalyzed)
                {
                    if (st.baitBlocks > 0) report.baitSteps++;
                    report.totalUnlockMoves += st.unlockMoves;
                }
                run = st.HasPressure(report.boardAnalyzed) ? 0 : run + 1;
                report.longestBreather = Math.Max(report.longestBreather, run);
            }
        }
    }
}
