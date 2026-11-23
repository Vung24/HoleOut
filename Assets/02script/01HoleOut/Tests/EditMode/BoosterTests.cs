using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using NUnit.Framework;
using static HoleOut.Tests.TestLevels;

namespace HoleOut.Tests
{
    public class BoosterTests
    {
        static int Count(GameSimulation sim, GameEventType t) => sim.Events.Count(e => e.Type == t);

        /// <summary>Khối R bị bốn khối B vây kín giữa bàn; cổng R ở cạnh trên.</summary>
        static GameSimulation BoxedIn(int dockSlots = 5, bool mystery = false)
        {
            var level = Level(new[] { "RRBB" }, 1, PictureDisplayMode.FullPicture, dockSlots,
                new List<GateData> { Gate("R", GateSide.Top, 4), Gate("B", GateSide.Bottom, 0) },
                new List<BlockData>
                {
                    Block(1, 2, 2, "R", 2, mystery: mystery),
                    Block(2, 2, 1, "B", 1), Block(3, 1, 2, "B", 1), Block(4, 3, 2, "B", 1), Block(5, 2, 3, "B", 1),
                    // Đủ 8 khối để khối bí ẩn không tự lộ màu theo luật số khối còn lại.
                    Block(6, 0, 0, "B", 1), Block(7, 5, 0, "B", 1), Block(8, 0, 3, "B", 1),
                });
            return new GameSimulation(level);
        }

        [Test]
        public void Lift_MovesBoxedBlockOutThroughNearestMatchingGate()
        {
            var sim = BoxedIn();
            Assert.IsFalse(ExitThrough(sim, 1, GateSide.Top), "bị vây thì không kéo ra được");

            Assert.IsTrue(sim.TryLiftBlock(1, out var side));
            Assert.AreEqual(GateSide.Top, side);
            Assert.IsNull(sim.Board.Find(1));
            Assert.AreEqual(1, Count(sim, GameEventType.BlockLifted));
            var hole = sim.Dock.Flying.Single();
            Assert.Less(hole.LaunchStart, Dock.FlightStart, "hố chờ khối bay qua bàn rồi mới cất cánh");

            Assert.IsTrue(RunUntil(sim, () => sim.Cleared == 2), "hai nhân vật R vào hố");
            Assert.AreEqual(7, sim.Board.Blocks.Count, "các khối B giữ nguyên chỗ");
        }

        [Test]
        public void Lift_RefusesMysteryBlock_FullDock_AndMissingGate()
        {
            Assert.IsFalse(BoxedIn(mystery: true).TryLiftBlock(1, out _), "khối bí ẩn chưa lộ màu");

            var noGate = new GameSimulation(Level(new[] { "G" }, 1, PictureDisplayMode.FullPicture, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0) }, new List<BlockData> { Block(1, 2, 2, "G", 1) }));
            Assert.IsFalse(noGate.TryLiftBlock(1, out _), "không có cổng cùng màu");

            var full = BoxedIn(dockSlots: 1);
            Assert.IsTrue(full.TryLiftBlock(1, out _));
            Assert.IsFalse(full.TryLiftBlock(2, out _), "khay đã đầy");
        }

        [Test]
        public void AddSlot_GrowsDockUpToLimit_AndNotWhileDragging()
        {
            var sim = BoxedIn(dockSlots: 4);
            Assert.IsTrue(sim.BeginDrag(2));
            Assert.IsFalse(sim.TryAddSlot(), "đang kéo khối");
            sim.CancelDrag();

            Assert.IsTrue(sim.TryAddSlot());
            Assert.AreEqual(5, sim.Dock.SlotCount);
            Assert.AreEqual(1, Count(sim, GameEventType.SlotAdded));
            Assert.IsTrue(sim.TryAddSlot());
            Assert.AreEqual(Dock.MaxSlotsLimit, sim.Dock.SlotCount);
            Assert.IsFalse(sim.TryAddSlot(), "đã tối đa");
        }

        [Test]
        public void AddSlot_KeepsRunnersHeadingToTheirHole()
        {
            var level = Level(new[] { "RRRR" }, 1, PictureDisplayMode.FullPicture, 4,
                new List<GateData> { Gate("R", GateSide.Top, 0) }, new List<BlockData> { Block(1, 0, 0, "R", 4) });
            var sim = new GameSimulation(level);
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Runners.Count > 0));
            Assert.IsTrue(sim.TryAddSlot());
            var hole = sim.Dock.Docked.Single();
            var slot = sim.Layout.DockSlot(hole.SlotIndex, sim.Dock.SlotCount);
            foreach (var r in sim.Runners)
            {
                Assert.AreEqual(slot.X, r.Target.X, 1e-9);
                Assert.AreEqual(slot.Z, r.Target.Z, 1e-9);
            }
            Assert.IsTrue(RunUntil(sim, () => sim.Status == GameStatus.Won));
        }

        /// <summary>Hàng R phía trên bị hàng B chắn lối; hố R sức chứa <paramref name="capacity"/>.</summary>
        static GameSimulation BlockedRow(string top, int capacity)
        {
            var level = Level(new[] { top, new string('B', top.Length) }, 2, PictureDisplayMode.FullPicture, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 5) },
                new List<BlockData> { Block(1, 0, 0, "R", capacity), Block(2, 5, 0, "B", top.Length) });
            var sim = new GameSimulation(level);
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Dock.Docked.Count == 1));
            return sim;
        }

        [Test]
        public void Magnet_PullsHiddenSameColorCharactersStraightIntoHole()
        {
            var sim = BlockedRow("RR", 2);
            sim.ClearEvents();
            sim.Step(0.5);
            Assert.AreEqual(0, sim.Runners.Count, "R bị B chắn, không tự ra được");

            Assert.AreEqual(2, sim.TryMagnet(1));
            Assert.AreEqual(1, Count(sim, GameEventType.MagnetPulled));
            Assert.IsTrue(sim.Runners.All(r => r.Lifted && r.Route.Count == 2 && !r.Tripped));
            Assert.IsTrue(RunUntil(sim, () => sim.Dock.Docked.Count == 0), "hố đủ người thì đóng");
            Assert.AreEqual(2, sim.Cleared);
        }

        [Test]
        public void Magnet_StopsAtHoleCapacity_AndFailsWithoutRoom()
        {
            var sim = BlockedRow("RRR", 2);
            Assert.AreEqual(2, sim.TryMagnet(1));
            Assert.AreEqual(4, sim.Picture.Remaining, "còn đúng một R và ba B trong tranh");
            Assert.AreEqual(0, sim.TryMagnet(1), "hố không còn chỗ trống");
            Assert.AreEqual(0, sim.TryMagnet(99), "hố không tồn tại");
        }
    }
}
