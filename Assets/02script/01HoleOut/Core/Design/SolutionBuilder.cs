using System.Collections.Generic;

namespace HoleOut.Core
{
    public struct SolutionBuildResult
    {
        public LevelSolution Solution;
        public string Error;
        public bool Success => Solution != null;
    }

    /// <summary>
    /// Dựng lời giải kéo-thả từ thứ tự đưa khối ra: với mỗi khối, dời ít nhất các khối khác để mở lối
    /// (<see cref="BoardReach.Unlock"/>), rồi đi đường ngắn nhất ra cổng. Kết quả cần kiểm chứng bằng
    /// <see cref="SolutionReplayer"/> trước khi dùng.
    /// </summary>
    public static class SolutionBuilder
    {
        /// <param name="order">Id khối (không phải chỉ số) theo thứ tự đưa ra.</param>
        public static SolutionBuildResult Build(LevelData level, IReadOnlyList<int> order, int boardStateLimit = 15000)
        {
            var steps = new List<SolutionStep>();
            var board = new List<BlockData>();
            int wave = -1;
            for (int k = 0; k < order.Count; k++)
            {
                if (board.Count == 0)
                {
                    wave++;
                    if (wave >= level.waves.Count) return Fail($"Thứ tự có {order.Count} khối, nhiều hơn số khối trong các đợt.");
                    foreach (var b in level.waves[wave].blocks) { var c = b.Copy(); c.mystery = false; board.Add(c); }
                }
                var target = board.Find(b => b.id == order[k]);
                if (target == null) return Fail($"Bước {k + 1}: khối {order[k]} không có trên bàn đợt {wave + 1}.");

                var moves = BoardReach.Unlock(target, board, level.gates, boardStateLimit);
                if (moves == null) return Fail($"Bước {k + 1}: khối {target.id} ({target.color}{target.capacity}) không ra được cổng.");
                foreach (var m in moves)
                    steps.Add(new SolutionStep { kind = SolutionStepKind.Move, block = m.Block, path = { new GridPoint(m.X, m.Y) } });

                var exit = BoardReach.ExitPath(target, board, level.gates);
                if (!exit.HasValue) return Fail($"Bước {k + 1}: khối {target.id} không tìm được đường ra cổng.");
                steps.Add(new SolutionStep { kind = SolutionStepKind.Exit, block = target.id, path = exit.Value.path, door = exit.Value.door });
                board.Remove(target);
            }
            if (board.Count > 0 || wave < level.waves.Count - 1) return Fail("Thứ tự chưa đưa hết khối ra.");
            return new SolutionBuildResult
            {
                Solution = new LevelSolution { levelId = level.levelId, verifiedMode = level.displayMode, steps = steps }
            };
        }

        static SolutionBuildResult Fail(string error) => new SolutionBuildResult { Error = error };
    }
}
