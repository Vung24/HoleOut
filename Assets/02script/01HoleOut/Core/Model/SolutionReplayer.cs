using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    public enum SolutionStepKind { Move, Exit }

    [Serializable]
    public struct GridPoint
    {
        public int x, y;
        public GridPoint(int x, int y) { this.x = x; this.y = y; }
        public override string ToString() => $"({x},{y})";
    }

    [Serializable]
    public class SolutionStep
    {
        public SolutionStepKind kind;
        public int block;
        /// <summary>Các ô liên tiếp (mỗi bước dịch đúng một ô) khối đi qua.</summary>
        public List<GridPoint> path = new List<GridPoint>();
        /// <summary>Cổng thoát khi kind = Exit.</summary>
        public GateSide door;
    }

    /// <summary>Lời giải gắn với phiên bản dữ liệu và chế độ hiển thị đã kiểm tra.</summary>
    [Serializable]
    public class LevelSolution
    {
        public string levelId;
        public int dataVersion;
        public PictureDisplayMode verifiedMode;
        public int seed = 1;
        public List<SolutionStep> steps = new List<SolutionStep>();
        public bool HasSteps => steps != null && steps.Count > 0;
    }

    public struct ReplayResult
    {
        public bool Success;
        public int StepsDone;
        public double SimTime;
        public GameStatus Status;
        public string Message;
        public override string ToString() => Success
            ? $"Đạt: {StepsDone} bước, {SimTime:0.0}s mô phỏng"
            : $"Lỗi ở bước {StepsDone}: {Message}";
    }

    /// <summary>
    /// Chạy lại lời giải qua API thao tác: kéo khối từng ô, kéo qua cổng để thoát,
    /// sau mỗi lần thoát chạy mô phỏng tới khi mọi chuyển động lắng xuống (có giới hạn).
    /// Không đặt thẳng khối tại đích và không bỏ qua va chạm/cổng.
    /// </summary>
    public static class SolutionReplayer
    {
        public const int IdleFramesToSettle = 10;

        public static ReplayResult Run(LevelData level, LevelSolution solution, double dt = 1 / 30.0, int? seed = null)
        {
            var sim = new GameSimulation(level, seed ?? solution.seed);
            return Run(sim, solution, dt);
        }

        public static ReplayResult Run(GameSimulation sim, LevelSolution solution, double dt)
        {
            var result = new ReplayResult();
            if (solution == null || !solution.HasSteps) return Fail(ref result, sim, "không có lời giải");
            int frameLimit = Math.Max(600, sim.Picture.Total * 20);

            for (int i = 0; i < solution.steps.Count; i++)
            {
                var step = solution.steps[i];
                result.StepsDone = i;
                var b = sim.Board.Find(step.block);
                if (b == null) return Fail(ref result, sim, $"không thấy khối {step.block} trên bàn");

                foreach (var p in step.path)
                {
                    if (Math.Abs(p.x - b.gx) + Math.Abs(p.y - b.gy) != 1)
                        return Fail(ref result, sim, $"khối {b.id} từ ({b.gx},{b.gy}) tới {p} không phải một ô");
                    if (!MoveOneCell(sim, b, p))
                        return Fail(ref result, sim, $"khối {b.id} không tới được {p}, dừng ở ({b.gx},{b.gy})");
                }

                if (step.kind == SolutionStepKind.Move) continue;

                if (!ExitThrough(sim, b, step.door))
                    return Fail(ref result, sim, $"khối {b.id} ({b.color}) không thoát được qua cổng {step.door} tại ({b.gx},{b.gy}); khay {sim.Dock.Occupied}/{sim.Dock.SlotCount}");

                if (!RunUntilSettled(sim, dt, frameLimit))
                    return Fail(ref result, sim, "mô phỏng không lắng xuống trong giới hạn");
                if (sim.Status == GameStatus.Jammed) return Fail(ref result, sim, "kẹt");
            }

            result.StepsDone = solution.steps.Count;
            if (sim.Status == GameStatus.Playing) RunUntilSettled(sim, dt, frameLimit);
            if (sim.Status != GameStatus.Won)
                return Fail(ref result, sim, $"chưa thắng sau lời giải: {sim.Cleared}/{sim.Picture.Total}");
            result.Success = true;
            result.Status = sim.Status;
            result.SimTime = sim.Time;
            return result;
        }

        static bool MoveOneCell(GameSimulation sim, BlockData b, GridPoint p)
        {
            if (!sim.BeginDrag(b.id)) return false;
            for (int k = 0; k < 20; k++)
            {
                var r = sim.DragTo(p.x, p.y);
                if (Math.Abs(r.X - p.x) < 1e-3 && Math.Abs(r.Y - p.y) < 1e-3) break;
            }
            sim.EndDrag();
            return b.gx == p.x && b.gy == p.y;
        }

        static bool ExitThrough(GameSimulation sim, BlockData b, GateSide side)
        {
            if (!sim.BeginDrag(b.id)) return false;
            double x = b.gx, y = b.gy;
            switch (side)
            {
                case GateSide.Top: y = -1; break;
                case GateSide.Bottom: y = DragSolver.BoardHeight - b.h + 1; break;
                case GateSide.Left: x = -1; break;
                default: x = DragSolver.BoardWidth - b.w + 1; break;
            }
            sim.DragTo(x, y);
            if (sim.DraggingBlockId == b.id) { sim.CancelDrag(); return false; }
            return true;
        }

        /// <summary>Lắng khi không còn hố bay/đóng, nhân vật chạy, tranh trượt hay nhân vật có thể xuất phát.</summary>
        static bool RunUntilSettled(GameSimulation sim, double dt, int frameLimit)
        {
            int idle = 0;
            for (int f = 0; f < frameLimit; f++)
            {
                sim.Step(dt);
                if (sim.Status != GameStatus.Playing) return true;
                idle = IsActive(sim) ? 0 : idle + 1;
                if (idle >= IdleFramesToSettle) return true;
            }
            return false;
        }

        static bool IsActive(GameSimulation sim)
        {
            if (sim.Dock.Flying.Count > 0 || sim.Runners.Count > 0 || sim.Dock.AnyClosing()) return true;
            var p = sim.Picture;
            if (!p.IsSettled || p.LowestOccupiedW() > p.RowOffset && p.Mode == PictureDisplayMode.WindowedScroll) return true;
            for (int w = p.WindowLow; w <= p.WindowHigh && w < p.Rows; w++)
                for (int c = 0; c < p.Columns; c++)
                    if (p.CanLeave(c, w))
                        foreach (var h in sim.Dock.Docked)
                            if (h.State == HoleState.Docked && h.Color == p.ColorAt(c, w) && h.Unclaimed > 0) return true;
            return false;
        }

        static ReplayResult Fail(ref ReplayResult r, GameSimulation sim, string message)
        {
            r.Success = false;
            r.Message = message;
            r.Status = sim.Status;
            r.SimTime = sim.Time;
            return r;
        }
    }
}
