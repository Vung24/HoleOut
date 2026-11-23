using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using NUnit.Framework;
using static HoleOut.Tests.TestLevels;

namespace HoleOut.Tests
{
    public class SimulationTests
    {
        static int Count(GameSimulation sim, GameEventType t) => sim.Events.Count(e => e.Type == t);

        [Test]
        public void Picture_OnlyBottomAndConnectedCellsAreExposed()
        {
            var level = Level(new[] { "AB", "CD" }, 2, PictureDisplayMode.WindowedScroll, 5, new List<GateData>(), new List<BlockData>());
            var p = new Picture(level);
            p.UpdateExposure();
            Assert.IsTrue(p.IsExposed(0, 0) && p.IsExposed(1, 0));
            Assert.IsFalse(p.IsExposed(0, 1) || p.IsExposed(1, 1));

            p.Remove(1, 0);
            p.UpdateExposure();
            Assert.IsTrue(p.IsExposed(1, 1), "ô trống mở đường cho ô phía trên");
            Assert.IsFalse(p.IsExposed(0, 1), "không có đường chéo");
        }

        [Test]
        public void Picture_ExitPathAvoidsSeatedCharacters()
        {
            var level = Level(new[] { "ABC", "DEF" }, 2, PictureDisplayMode.WindowedScroll, 5, new List<GateData>(), new List<BlockData>());
            var p = new Picture(level);
            p.Remove(2, 0); // F
            p.Remove(1, 1); // B
            p.Remove(2, 1); // C
            p.UpdateExposure();
            Assert.IsTrue(p.IsExposed(0, 1), "A nối ra qua ô B, C, F đã trống");
            var path = p.FindExitPath(0, 1);
            CollectionAssert.AreEqual(new[] { (1, 1), (2, 1), (2, 0) }, path);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Runner_UsesStraightRouteOnlyWhenClear(bool blocked)
        {
            var level = Level(new[] { "BBBBBBR", "BBBBBBB", "BBBBBBB", "BBBBBBB" }, 4, PictureDisplayMode.FullPicture, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0) },
                new List<BlockData> { Block(1, 0, 0, "R", 1) });
            var sim = new GameSimulation(level);
            for (int w = 0; w < sim.Picture.Rows; w++)
                for (int c = 0; c < sim.Picture.Columns; c++)
                    if ((c, w) != (6, 3) && (!blocked || (c, w) != (5, 2)))
                        sim.Picture.Remove(c, w);

            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Runners.Count == 1));
            var runner = sim.Runners[0];
            Assert.AreEqual(blocked ? 6 : 2, runner.Route.Count);
            if (blocked)
                Assert.AreEqual(runner.Route[0].X, runner.Route[1].X, 1e-9);
            else
                Assert.AreEqual(Vec2.Distance(runner.Route[0], runner.Entry) - MotionConfig.LedgeOffset + MotionConfig.RouteOvershoot,
                    runner.RunDistance, 1e-9);
        }

        [Test]
        public void Picture_WindowAdvancesOverSeveralClearRows()
        {
            var level = Level(new[] { "AA", "BB", "CC", "DD" }, 1, PictureDisplayMode.WindowedScroll, 5, new List<GateData>(), new List<BlockData>());
            var p = new Picture(level);
            for (int c = 0; c < 2; c++) { p.Remove(c, 0); p.Remove(c, 1); }
            Assert.IsTrue(p.AdvanceWindow());
            Assert.AreEqual(2, p.RowOffset);
            Assert.IsFalse(p.IsSettled, "tranh còn đang trượt");
            p.AnimateScroll(1);
            Assert.IsTrue(p.IsSettled);
        }

        [Test]
        public void Picture_RemovingPartOfRowDoesNotScrollOrCompactColumn()
        {
            var level = Level(new[] { "AA", "BB" }, 1, PictureDisplayMode.WindowedScroll, 5, new List<GateData>(), new List<BlockData>());
            var p = new Picture(level);
            p.Remove(0, 0);
            Assert.IsFalse(p.AdvanceWindow());
            Assert.AreEqual("A", p.ColorAt(0, 1), "nhân vật giữ hàng nguồn");
            p.UpdateExposure();
            Assert.IsFalse(p.IsExposed(0, 1), "hàng trên nằm ngoài cửa sổ 1 hàng");
        }

        [Test]
        public void FullPicture_ShowsAllRowsAndNeverScrolls()
        {
            var level = Level(new[] { "AA", "BB", "CC" }, 1, PictureDisplayMode.FullPicture, 5, new List<GateData>(), new List<BlockData>());
            var p = new Picture(level);
            Assert.AreEqual(3, p.VisibleRows);
            p.Remove(0, 0); p.Remove(1, 0);
            Assert.IsFalse(p.AdvanceWindow());
            p.UpdateExposure();
            Assert.IsTrue(p.IsExposed(0, 1));
        }

        static LevelData OneRowLevel(int dockSlots = 5) => Level(new[] { "RRBB" }, 1, PictureDisplayMode.WindowedScroll, dockSlots,
            new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2) },
            new List<BlockData> { Block(1, 0, 0, "R", 2), Block(2, 2, 0, "B", 2) });

        [Test]
        public void FullLoop_ExitBothBlocks_Wins()
        {
            var sim = new GameSimulation(OneRowLevel());
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Status != GameStatus.Playing));
            Assert.AreEqual(GameStatus.Won, sim.Status);
            Assert.AreEqual(4, sim.Cleared);
            Assert.AreEqual(1, Count(sim, GameEventType.Won));
            Assert.AreEqual(2, Count(sim, GameEventType.HoleReleased));
            sim.Step(1);
            Assert.AreEqual(1, Count(sim, GameEventType.Won), "Won phát một lần");
        }

        [Test]
        public void FlyingHole_OccupiesDockSlot()
        {
            var sim = new GameSimulation(OneRowLevel(dockSlots: 1));
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.AreEqual(1, sim.Dock.Flying.Count);
            Assert.IsFalse(ExitThrough(sim, 2, GateSide.Top), "khay đã có hố đang bay");
            Assert.IsNotNull(sim.Board.Find(2));
        }

        [Test]
        public void HoleClosing_DefersEndAndReleasesSlotAfterwards()
        {
            var sim = new GameSimulation(OneRowLevel());
            ExitThrough(sim, 1, GateSide.Top);
            ExitThrough(sim, 2, GateSide.Top);
            Assert.IsTrue(RunUntil(sim, () => sim.Picture.Remaining == 0 && sim.Runners.Count == 0));
            Assert.IsTrue(RunUntil(sim, () => sim.Dock.AnyClosing() || sim.Status == GameStatus.Won, 1));
            if (sim.Dock.AnyClosing()) Assert.AreEqual(GameStatus.Playing, sim.Status, "hoãn thắng khi hố đang đóng");
            Assert.IsTrue(RunUntil(sim, () => sim.Status == GameStatus.Won, 2));
            Assert.AreEqual(0, sim.Dock.Occupied);
        }

        [Test]
        public void Jam_ThenContinueWithExtraSlot_ThenWin()
        {
            // Tranh 1 cột: R ở trên, B ở dưới. Đưa hố R lên khay 1 chỗ trước → kẹt.
            var level = Level(new[] { "R", "B" }, 2, PictureDisplayMode.WindowedScroll, 1,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2) },
                new List<BlockData> { Block(1, 0, 0, "R", 1), Block(2, 2, 0, "B", 1) });
            var sim = new GameSimulation(level);
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Status != GameStatus.Playing, 5));
            Assert.AreEqual(GameStatus.Jammed, sim.Status);
            Assert.AreEqual(1, Count(sim, GameEventType.Jammed));
            Assert.IsFalse(sim.BeginDrag(2), "không thao tác khi kẹt");

            Assert.IsTrue(sim.TryContinueWithExtraSlot());
            Assert.AreEqual(GameStatus.Playing, sim.Status);
            Assert.AreEqual(2, sim.Dock.SlotCount);
            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Status != GameStatus.Playing));
            Assert.AreEqual(GameStatus.Won, sim.Status);
        }

        [Test]
        public void Continue_FailsWhenNotJammedOrDockAtMax()
        {
            var sim = new GameSimulation(OneRowLevel(dockSlots: Dock.MaxSlotsLimit));
            Assert.IsFalse(sim.TryContinueWithExtraSlot());
            Assert.IsFalse(sim.Dock.AddSlot());
        }

        [Test]
        public void SameColorHoles_ServedInDockOrder()
        {
            var level = Level(new[] { "RRR" }, 1, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0, 6) },
                new List<BlockData> { Block(1, 0, 0, "R", 2), Block(2, 1, 0, "R", 1) });
            var sim = new GameSimulation(level);
            ExitThrough(sim, 1, GateSide.Top);
            ExitThrough(sim, 2, GateSide.Top);
            Assert.IsTrue(RunUntil(sim, () => sim.Status == GameStatus.Won));
            var landed = sim.Events.Where(e => e.Type == GameEventType.RunnerLanded).Select(e => e.Value).ToList();
            Assert.AreEqual(2, landed.Count(h => h == 1));
            Assert.AreEqual(1, landed.Count(h => h == 2));
        }

        [Test]
        public void Reservation_NeverExceedsCapacity_AndLanesNeverOverlap()
        {
            var grid = new[] { "RRRRRRRRRR", "RRRRRRRRRR", "RRRRRRRRRR" };
            var level = Level(grid, 3, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0, 6) },
                new List<BlockData> { Block(1, 0, 0, "R", 30) });
            var sim = new GameSimulation(level, seed: 7);
            ExitThrough(sim, 1, GateSide.Top);
            var seen = new Dictionary<int, Runner>();
            int maxRunners = 0;
            Assert.IsTrue(RunUntil(sim, () =>
            {
                foreach (var h in sim.Dock.Docked)
                    Assert.LessOrEqual(h.Held + h.Incoming, h.RemainingCapacity, "giữ + đang tới ≤ sức chứa");
                foreach (var r in sim.Runners) seen[r.Id] = r;
                maxRunners = System.Math.Max(maxRunners, sim.Runners.Count);
                return sim.Status != GameStatus.Playing;
            }));
            Assert.AreEqual(GameStatus.Won, sim.Status);
            Assert.AreEqual(30, seen.Count);
            Assert.LessOrEqual(maxRunners, GameSimulation.MaxRunnersPerHole);
            foreach (var lane in seen.Values.GroupBy(r => r.EntryLane))
            {
                var ordered = lane.OrderBy(r => r.AirStartAt).ToList();
                for (int i = 1; i < ordered.Count; i++)
                    Assert.GreaterOrEqual(ordered[i].AirStartAt + 1e-9, ordered[i - 1].AirEndAt + MotionConfig.LaneGap,
                        "cùng làn: lượt sau bay sau khi lượt trước đáp + 0,04 giây");
            }
        }

        [Test]
        public void WindowedPicture_ScrollsAfterBottomRowCleared()
        {
            var level = Level(new[] { "BB", "RR" }, 1, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2) },
                new List<BlockData> { Block(1, 0, 0, "R", 2), Block(2, 2, 0, "B", 2) });
            var sim = new GameSimulation(level);
            ExitThrough(sim, 2, GateSide.Top); // B chưa lộ vì nằm sau tấm che
            ExitThrough(sim, 1, GateSide.Top);
            Assert.IsTrue(RunUntil(sim, () => sim.Picture.RowOffset == 1, 10));
            Assert.AreEqual(1, Count(sim, GameEventType.PictureScrolled));
            Assert.IsTrue(RunUntil(sim, () => sim.Status != GameStatus.Playing));
            Assert.AreEqual(GameStatus.Won, sim.Status);
        }

        [Test]
        public void WindowedPicture_NextRowDepartsBeforePreviousRowLands()
        {
            var level = Level(new[] { "BB", "RR" }, 1, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2) },
                new List<BlockData> { Block(1, 0, 0, "R", 2), Block(2, 2, 0, "B", 2) });
            var sim = new GameSimulation(level);
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Runners.Any(r => r.Color == "R")));
            var firstRunner = sim.Runners.First(r => r.Color == "R");
            var route = firstRunner.Route.ToArray();

            Assert.IsTrue(RunUntil(sim, () => sim.Runners.Any(r => r.Color == "B")));
            Assert.AreEqual(1, sim.Picture.RowOffset);
            Assert.IsTrue(sim.Picture.IsSettled);
            Assert.AreEqual(0, Count(sim, GameEventType.RunnerLanded), "hàng mới xuất phát trước khi nhóm trước đáp");
            CollectionAssert.AreEqual(route, firstRunner.Route, "trượt tranh giữ nguyên đường chạy đã lập");
            Assert.IsTrue(RunUntil(sim, () => sim.Status == GameStatus.Won));
            Assert.AreEqual(4, sim.Cleared);
        }

        [Test]
        public void Waves_NextWaveSpawnsWhenLastBlockLeaves()
        {
            var level = Level(new[] { "RB" }, 1, PictureDisplayMode.WindowedScroll, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2) },
                new List<BlockData> { Block(1, 0, 0, "R", 1) },
                new List<BlockData> { Block(2, 2, 0, "B", 1) });
            var sim = new GameSimulation(level);
            Assert.IsNull(sim.Board.Find(2));
            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.IsNotNull(sim.Board.Find(2), "đợt sau xuất hiện ngay, không chờ hố thu nhận xong");
            Assert.AreEqual(2, sim.Board.WaveNumber);
            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.IsTrue(RunUntil(sim, () => sim.Status == GameStatus.Won));
        }

        [Test]
        public void SameSeed_SameEventLog_AcrossTickRates()
        {
            string Run(int seed, double dt)
            {
                var level = Level(new[] { "RRRRRR", "BBBBBB", "RRRRRR" }, 2, PictureDisplayMode.WindowedScroll, 5,
                    new List<GateData> { Gate("R", GateSide.Top, 0, 2), Gate("B", GateSide.Top, 2, 2) },
                    new List<BlockData> { Block(1, 0, 0, "R", 6), Block(2, 2, 0, "B", 6), Block(3, 1, 1, "R", 6) });
                var sim = new GameSimulation(level, seed);
                ExitThrough(sim, 1, GateSide.Top);
                ExitThrough(sim, 2, GateSide.Top);
                RunUntil(sim, () => sim.Board.Find(3) != null && sim.Dock.Occupied < 5 && ExitThrough(sim, 3, GateSide.Top), 1, dt);
                RunUntil(sim, () => sim.Status != GameStatus.Playing, 60, dt);
                Assert.AreEqual(GameStatus.Won, sim.Status, "dt=" + dt);
                return string.Join(",", sim.Events.Where(e => e.Type == GameEventType.RunnerLanded).Select(e => e.Id));
            }
            Assert.AreEqual(Run(3, 1 / 60.0), Run(3, 1 / 60.0));
            Run(3, 1 / 20.0); // frame chậm vẫn hoàn thành màn
        }
    }
}
