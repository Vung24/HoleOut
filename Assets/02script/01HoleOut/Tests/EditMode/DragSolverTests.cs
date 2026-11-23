using System.Collections.Generic;
using HoleOut.Core;
using NUnit.Framework;
using static HoleOut.Tests.TestLevels;

namespace HoleOut.Tests
{
    public class DragSolverTests
    {
        static GameSimulation Sim(List<GateData> gates, int dockSlots, params BlockData[] blocks) =>
            new GameSimulation(Level(new[] { "RRRR" }, 1, PictureDisplayMode.WindowedScroll, dockSlots, gates, new List<BlockData>(blocks)));

        [Test]
        public void LBlock_OnlyOccupiesItsThreeCells()
        {
            var corner = Block(1, 1, 1, "R", 1, w: 2, h: 2);
            corner.shape = BlockShape.L;
            var single = Block(2, 2, 1, "B", 1);
            var blocks = new List<BlockData> { corner, single };

            Assert.IsFalse(corner.ContainsCell(1.5, 0.5));
            Assert.IsTrue(corner.ContainsCell(1.5, 1.5));
            Assert.IsTrue(DragSolver.Free(corner, 1, 1, blocks));
            Assert.IsFalse(DragSolver.Free(corner, 2, 1, blocks));
            Assert.IsFalse(DragSolver.Free(single, 2, 2, blocks));
        }

        [Test]
        public void LBlock_NeedsTwoCellGate()
        {
            var corner = Block(1, 0, 0, "R", 1, w: 2, h: 2);
            corner.shape = BlockShape.L;
            var blocks = new List<BlockData> { corner };
            var gates = new List<GateData> { Gate("R", GateSide.Top, 0) };
            Assert.IsNull(DragSolver.Drag(corner, blocks, gates, 0, 0, 0, -1, false).Exit);
            gates[0].len = 2;
            Assert.AreSame(gates[0], DragSolver.Drag(corner, blocks, gates, 0, 0, 0, -1, false).Exit);
        }

        [Test]
        public void Drag_StopsAtObstacle_NoTunnelling()
        {
            var a = Block(1, 0, 0, "R", 1);
            var b = Block(2, 2, 0, "B", 1);
            var r = DragSolver.Drag(a, new List<BlockData> { a, b }, new List<GateData>(), 0, 0, 5, 0, false);
            Assert.AreEqual(1, r.SnapX);
            Assert.AreEqual(0, r.SnapY);
            Assert.IsTrue(r.Blocked);
        }

        [Test]
        public void Simulation_DragFollowsPointerAfterClearingObstacle()
        {
            var sim = Sim(new List<GateData>(), 5, Block(1, 0, 0, "R", 1), Block(2, 2, 0, "B", 1));
            Assert.IsTrue(sim.BeginDrag(1));

            var stopped = sim.DragTo(5, 0);
            Assert.IsTrue(stopped.Blocked);
            Assert.AreEqual(1, stopped.SnapX);
            Assert.LessOrEqual(stopped.VisualX, 1.0001);
            Assert.AreEqual(stopped.X, sim.DragTo(5, 0).X, 1e-4);

            var partial = sim.DragTo(5, 0.5);
            Assert.IsTrue(partial.Blocked);
            Assert.LessOrEqual(partial.VisualX, 1.0001);

            var clear = sim.DragTo(5, 1);
            Assert.IsFalse(clear.Blocked);
            Assert.AreEqual(5, clear.VisualX, 1e-4);
            Assert.AreEqual(1, clear.VisualY, 1e-4);
            Assert.IsTrue(sim.EndDrag());
            Assert.AreEqual(5, sim.Board.Find(1).gx);
            Assert.AreEqual(1, sim.Board.Find(1).gy);
        }

        [Test]
        public void Simulation_DragCannotExitThroughObstacle()
        {
            var sim = Sim(new List<GateData> { Gate("R", GateSide.Right, 0) }, 5,
                Block(1, 0, 0, "R", 1), Block(2, 2, 0, "B", 1));
            Assert.IsTrue(sim.BeginDrag(1));

            var stopped = sim.DragTo(6, 0);
            Assert.IsNull(stopped.Exit);
            Assert.IsTrue(stopped.Blocked);
            Assert.AreEqual(1, sim.DraggingBlockId);
            Assert.AreEqual(0, sim.Dock.Flying.Count);

            Assert.AreEqual(5, sim.DragTo(5, 1).SnapX);
            Assert.IsNotNull(sim.DragTo(6, 0).Exit);
            Assert.AreEqual(1, sim.Dock.Flying.Count);
        }

        [Test]
        public void Drag_SlidesAlongWall()
        {
            var a = Block(1, 0, 0, "R", 1);
            var r = DragSolver.Drag(a, new List<BlockData> { a }, new List<GateData>(), 0, 0, 3, -2, false);
            Assert.AreEqual(3, r.SnapX);
            Assert.AreEqual(0, r.SnapY);
        }

        [Test]
        public void Drag_CornerAssist_EntersLane()
        {
            // Khối lệch 0,3 ô so với làn trống giữa hai khối chặn; ngón tay kéo dọc theo làn.
            var a = Block(1, 0, 1, "R", 1);
            var top = Block(2, 1, 0, "B", 1);
            var bottom = Block(3, 1, 2, "B", 1);
            var blocks = new List<BlockData> { a, top, bottom };
            // Mỗi lần di ngón tay gọi lại solver từ vị trí trước đó.
            var r = DragSolver.Drag(a, blocks, new List<GateData>(), 0, 1.3, 3, 1, false);
            for (int i = 0; i < 10; i++) r = DragSolver.Drag(a, blocks, new List<GateData>(), r.X, r.Y, 3, 1, false);
            Assert.AreEqual(3, r.SnapX);
            Assert.AreEqual(1, r.SnapY);
        }

        [Test]
        public void Exit_RequiresCrossingThreshold()
        {
            var gates = new List<GateData> { Gate("R", GateSide.Top, 0) };
            var a = Block(1, 0, 0, "R", 1);
            var blocks = new List<BlockData> { a };
            Assert.IsNull(DragSolver.Drag(a, blocks, gates, 0, 0, 0, -0.5, false).Exit);
            Assert.IsNotNull(DragSolver.Drag(a, blocks, gates, 0, 0, 0, -0.53, false).Exit);
        }

        [Test]
        public void Exit_RequiresMatchingColor()
        {
            var gates = new List<GateData> { Gate("B", GateSide.Top, 0) };
            var a = Block(1, 0, 0, "R", 1);
            var r = DragSolver.Drag(a, new List<BlockData> { a }, gates, 0, 0, 0, -1, false);
            Assert.IsNull(r.Exit);
            Assert.IsTrue(r.Blocked);
        }

        [Test]
        public void Exit_GateWidthMeasuredPerpendicular()
        {
            var a = Block(1, 0, 0, "R", 1, w: 2, h: 1);
            var blocks = new List<BlockData> { a };
            Assert.IsNull(DragSolver.Drag(a, blocks, new List<GateData> { Gate("R", GateSide.Top, 0, 1) }, 0, 0, 0, -1, false).Exit);
            Assert.IsNotNull(DragSolver.Drag(a, blocks, new List<GateData> { Gate("R", GateSide.Top, 0, 2) }, 0, 0, 0, -1, false).Exit);
            // Thoát sang trái chỉ cần bề cao 1.
            Assert.IsNotNull(DragSolver.Drag(a, blocks, new List<GateData> { Gate("R", GateSide.Left, 0, 1) }, 0, 0, -1, 0, false).Exit);
        }

        [Test]
        public void Exit_SmallBlockUsesAnyLaneOfWideGate()
        {
            var gates = new List<GateData> { Gate("R", GateSide.Top, 2, 3) };
            foreach (int x in new[] { 2, 3, 4 })
            {
                var a = Block(1, x, 0, "R", 1);
                Assert.IsNotNull(DragSolver.Drag(a, new List<BlockData> { a }, gates, x, 0, x, -1, false).Exit, "làn " + x);
            }
            var outside = Block(1, 1, 0, "R", 1);
            Assert.IsNull(DragSolver.Drag(outside, new List<BlockData> { outside }, gates, 1, 0, 1, -1, false).Exit);
        }

        [Test]
        public void Exit_BlockedWhenDockFull()
        {
            var a = Block(1, 0, 0, "R", 1);
            var r = DragSolver.Drag(a, new List<BlockData> { a }, new List<GateData> { Gate("R", GateSide.Top, 0) }, 0, 0, 0, -1, true);
            Assert.IsNull(r.Exit);
            Assert.IsTrue(r.Blocked);
        }

        [TestCase(GateSide.Top)]
        [TestCase(GateSide.Bottom)]
        [TestCase(GateSide.Left)]
        [TestCase(GateSide.Right)]
        public void Exit_WideGateAcceptsContinuousPosition(GateSide side)
        {
            bool horizontal = side == GateSide.Left || side == GateSide.Right;
            foreach (double offset in new[] { 0.25, 0.5, 0.75 })
            {
                double x = horizontal ? (side == GateSide.Left ? 0 : 5) : 1 + offset;
                double y = horizontal ? 1 + offset : (side == GateSide.Top ? 0 : 3);
                double tx = x + (side == GateSide.Left ? -1 : side == GateSide.Right ? 1 : 0);
                double ty = y + (side == GateSide.Top ? -1 : side == GateSide.Bottom ? 1 : 0);
                var a = Block(1, (int)x, (int)y, "R", 1);
                var gates = new List<GateData> { Gate("R", side, 1, 2) };
                var r = DragSolver.Drag(a, new List<BlockData> { a }, gates, x, y, tx, ty, false);
                Assert.AreSame(gates[0], r.Exit);
                Assert.IsFalse(r.Blocked);
                Assert.AreEqual(horizontal ? y : x, horizontal ? r.VisualY : r.VisualX, 1e-6);
            }
        }

        [TestCase("B", false, 1.5)]
        [TestCase("R", true, 1.5)]
        [TestCase("R", false, 0.5)]
        [TestCase("R", false, 2.5)]
        public void Exit_FractionalPositionStillRespectsGateRules(string color, bool full, double x)
        {
            var a = Block(1, 1, 0, "R", 1);
            var gates = new List<GateData> { Gate(color, GateSide.Top, 1, 2) };
            var r = DragSolver.Drag(a, new List<BlockData> { a }, gates, x, 0, x, -1, full);
            Assert.IsNull(r.Exit);
            Assert.IsTrue(r.Blocked);
        }

        [Test]
        public void Simulation_ExitsThroughMiddleOfWideGate()
        {
            var sim = Sim(new List<GateData> { Gate("R", GateSide.Top, 1, 2) }, 5, Block(1, 1, 0, "R", 4));
            Assert.IsTrue(sim.BeginDrag(1));
            var preview = sim.DragTo(1.5, -0.3);
            Assert.IsNull(preview.Exit);
            Assert.AreEqual(1.5, preview.VisualX, 1e-6);
            Assert.AreEqual(-0.3, preview.VisualY, 1e-6);
            var exit = sim.DragTo(1.5, -0.6);
            Assert.IsNotNull(exit.Exit);
            Assert.AreEqual(1.5, exit.VisualX, 1e-6);
            Assert.IsNull(sim.Board.Find(1));
            Assert.AreEqual(-1, sim.DraggingBlockId);
            Assert.AreEqual(1, sim.Dock.Flying.Count);
        }

        [Test]
        public void Mystery_CannotExitUntilRevealed()
        {
            var blocks = new List<BlockData>
            {
                Block(0, 0, 0, "R", 1, mystery: true),
                Block(1, 1, 0, "B", 1), Block(2, 0, 1, "B", 1),
                Block(3, 3, 0, "B", 1), Block(4, 3, 1, "B", 1), Block(5, 3, 2, "B", 1),
                Block(6, 5, 0, "B", 1), Block(7, 5, 1, "B", 1),
            };
            var sim = new GameSimulation(Level(new[] { "RBBBBBBB" }, 1, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 1) }, blocks));
            Assert.IsTrue(sim.Board.Find(0).mystery, "khối bị bao kín, bàn còn 8 khối");
            Assert.IsFalse(ExitThrough(sim, 0, GateSide.Top));

            // Khối bên cạnh rời bàn: khối bí ẩn dịch được nên lộ màu.
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsFalse(sim.Board.Find(0).mystery);
            Assert.IsTrue(ExitThrough(sim, 0, GateSide.Top));
        }

        [Test]
        public void Simulation_DragSnapsToNearestCellOnRelease()
        {
            var sim = Sim(new List<GateData>(), 5, Block(1, 0, 0, "R", 4));
            sim.BeginDrag(1);
            sim.DragTo(2.4, 1.6);
            sim.EndDrag();
            var b = sim.Board.Find(1);
            Assert.AreEqual(2, b.gx);
            Assert.AreEqual(2, b.gy);
        }
    }
}
