using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using NUnit.Framework;
using static HoleOut.Tests.TestLevels;

namespace HoleOut.Tests
{
    public class IceBlockTests
    {
        static BlockData Frozen(int id, int gx, int gy, string color, int capacity, int ice)
        {
            var b = Block(id, gx, gy, color, capacity);
            b.ice = ice;
            return b;
        }

        /// <summary>Khối R băng 2 lớp ở góc trái; hai khối B đi thẳng lên cổng B.</summary>
        static LevelData ThreeBlocks() => Level(new[] { "RBB" }, 1, PictureDisplayMode.FullPicture, 5,
            new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2, 3) },
            new List<BlockData> { Frozen(0, 0, 0, "R", 1, 2), Block(1, 2, 0, "B", 1), Block(2, 4, 0, "B", 1) });

        [Test]
        public void Frozen_CannotMoveUntilEnoughOtherBlocksExit()
        {
            var sim = new GameSimulation(ThreeBlocks());
            Assert.IsFalse(sim.BeginDrag(0), "còn băng thì không chọn được");

            Assert.IsTrue(ExitThrough(sim, 1, GateSide.Top));
            Assert.AreEqual(1, sim.Board.Find(0).ice);
            var cracked = sim.Events.Single(e => e.Type == GameEventType.BlockIceCracked);
            Assert.AreEqual(0, cracked.Id);
            Assert.AreEqual(1, cracked.Value);
            Assert.IsFalse(sim.BeginDrag(0));

            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.AreEqual(0, sim.Board.Find(0).ice);
            Assert.AreEqual(1, sim.Events.Count(e => e.Type == GameEventType.BlockThawed && e.Id == 0));
            Assert.IsTrue(ExitThrough(sim, 0, GateSide.Top), "tan băng thì kéo ra cổng như khối thường");
        }

        [Test]
        public void Frozen_InNextWave_IsNotThawedByTheExitThatSpawnsIt()
        {
            var level = Level(new[] { "RBB" }, 1, PictureDisplayMode.FullPicture, 5,
                new List<GateData> { Gate("R", GateSide.Top, 0), Gate("B", GateSide.Top, 2, 3) },
                new List<BlockData> { Block(0, 0, 0, "R", 1) },
                new List<BlockData> { Frozen(1, 2, 0, "B", 1, 1), Block(2, 4, 0, "B", 1) });
            var sim = new GameSimulation(level);

            Assert.IsTrue(ExitThrough(sim, 0, GateSide.Top));
            Assert.AreEqual(2, sim.Board.WaveNumber);
            Assert.AreEqual(1, sim.Board.Find(1).ice, "đợt sau chưa lên bàn khi khối kia rời bàn");

            Assert.IsTrue(ExitThrough(sim, 2, GateSide.Top));
            Assert.AreEqual(0, sim.Board.Find(1).ice);
        }

        [Test]
        public void Lift_RefusesFrozenBlock()
        {
            var sim = new GameSimulation(ThreeBlocks());
            Assert.IsFalse(sim.TryLiftBlock(0, out _));
            Assert.IsTrue(sim.TryLiftBlock(1, out _));
            Assert.AreEqual(1, sim.Board.Find(0).ice, "khối được nhấc ra cũng tính một lần rời bàn");
        }

        [Test]
        public void DrainModel_OffersFrozenBlockOnlyAfterEnoughExits()
        {
            var model = new DrainModel(ThreeBlocks());
            var s = model.Initial();
            var choices = new List<int>();
            model.Choices(s, choices);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, choices);
            Assert.IsFalse(model.CanLaunch(s, 0));

            model.LaunchInPlace(s, 1);
            Assert.IsFalse(model.CanLaunch(s, 0));
            model.LaunchInPlace(s, 2);
            Assert.IsTrue(model.CanLaunch(s, 0));
        }

        [Test]
        public void SolutionBuilder_RejectsOrderThatPullsFrozenBlockTooEarly()
        {
            var level = ThreeBlocks();
            Assert.IsFalse(SolutionBuilder.Build(level, new[] { 0, 1, 2 }).Success);
            var ok = SolutionBuilder.Build(level, new[] { 1, 2, 0 });
            Assert.IsTrue(ok.Success, ok.Error);
            Assert.IsTrue(SolutionReplayer.Run(level, ok.Solution).Success);
        }

        [Test]
        public void Validator_RequiresAMovableBlockAndIceBelowWaveSize()
        {
            Assert.IsEmpty(LevelValidator.Validate(ThreeBlocks()));

            var tooMuch = ThreeBlocks();
            tooMuch.waves[0].blocks[0].ice = 3;
            Assert.IsTrue(LevelValidator.Validate(tooMuch).Any(e => e.Contains("lớp băng")));

            var allFrozen = ThreeBlocks();
            foreach (var b in allFrozen.waves[0].blocks) b.ice = 1;
            Assert.IsTrue(LevelValidator.Validate(allFrozen).Any(e => e.Contains("đóng băng")));
        }
    }
}
