using System.Collections.Generic;
using HoleOut.Core;

namespace HoleOut.Tests
{
    static class TestLevels
    {
        public static BlockData Block(int id, int gx, int gy, string color, int capacity, int w = 1, int h = 1, bool mystery = false) =>
            new BlockData { id = id, gx = gx, gy = gy, w = w, h = h, color = color, capacity = capacity, mystery = mystery };

        public static GateData Gate(string color, GateSide side, int start, int len = 1) =>
            new GateData { color = color, side = side, start = start, len = len };

        public static LevelData Level(string[] grid, int visibleRows, PictureDisplayMode mode, int dockSlots,
            List<GateData> gates, params List<BlockData>[] waves)
        {
            var level = new LevelData
            {
                levelId = "test", grid = grid, visibleRows = visibleRows, displayMode = mode,
                dockSlots = dockSlots, gates = gates
            };
            foreach (var w in waves) level.waves.Add(new WaveData { blocks = w });
            return level;
        }

        /// <summary>Kéo khối liên tục tới ô (x, y) bằng nhiều bước nhỏ, rồi thả.</summary>
        public static void DragAlong(GameSimulation sim, int blockId, params (double x, double y)[] points)
        {
            if (!sim.BeginDrag(blockId)) throw new System.InvalidOperationException("Không chọn được khối " + blockId);
            foreach (var (x, y) in points)
            {
                for (int i = 0; i < 40 && sim.DraggingBlockId == blockId; i++)
                {
                    var r = sim.DragTo(x, y);
                    if (System.Math.Abs(r.X - x) < 1e-3 && System.Math.Abs(r.Y - y) < 1e-3) break;
                }
                if (sim.DraggingBlockId != blockId) return;
            }
            sim.EndDrag();
        }

        /// <summary>Đưa khối ra cổng: kéo vượt biên 1 ô theo hướng cổng.</summary>
        public static bool ExitThrough(GameSimulation sim, int blockId, GateSide side)
        {
            var b = sim.Board.Find(blockId);
            if (b == null || !sim.BeginDrag(blockId)) return false;
            double x = b.gx, y = b.gy;
            switch (side)
            {
                case GateSide.Top: y = -1; break;
                case GateSide.Bottom: y = DragSolver.BoardHeight - b.h + 1; break;
                case GateSide.Left: x = -1; break;
                default: x = DragSolver.BoardWidth - b.w + 1; break;
            }
            sim.DragTo(x, y);
            if (sim.DraggingBlockId == blockId) { sim.EndDrag(); return false; }
            return true;
        }

        /// <summary>Chạy mô phỏng tới khi điều kiện đạt hoặc hết thời gian mô phỏng.</summary>
        public static bool RunUntil(GameSimulation sim, System.Func<bool> condition, double timeout = 60, double dt = 1 / 60.0)
        {
            double end = sim.Time + timeout;
            while (sim.Time < end)
            {
                if (condition()) return true;
                sim.Step(dt);
            }
            return condition();
        }
    }
}
