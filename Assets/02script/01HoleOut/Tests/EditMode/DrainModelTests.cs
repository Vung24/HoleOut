using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HoleOut.Tests.TestLevels;

namespace HoleOut.Tests
{
    /// <summary>Mô hình nhanh phải cho cùng kết quả với mô phỏng thật.</summary>
    public class DrainModelTests
    {
        const string Folder = "Assets/07data/HoleOutTestLevels";

        static IEnumerable<string> LevelPaths() =>
            AssetDatabase.FindAssets("t:LevelSO", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

        static LevelData TwoRows(int slots) => Level(
            new[] { "AA", "BB" }, 2, PictureDisplayMode.FullPicture, slots,
            new List<GateData>(), new List<BlockData> { Block(0, 0, 0, "A", 2), Block(1, 1, 0, "B", 2) });

        [Test]
        public void FrontColorFirst_Wins_BackColorFirst_JamsWithOneSlot()
        {
            var model = new DrainModel(TwoRows(1));
            Assert.AreEqual(DrainStatus.Won, model.Play(new[] { 1, 0 }).Status);
            var jam = model.Play(new[] { 0, 1 });
            Assert.AreEqual(DrainStatus.Jammed, jam.Status);
            Assert.AreEqual(4, jam.Remaining);
        }

        [Test]
        public void HoleWaitsWhenSlotsAllow()
        {
            var model = new DrainModel(TwoRows(2));
            Assert.AreEqual(DrainStatus.Won, model.Play(new[] { 0, 1 }).Status);
        }

        [Test]
        public void SameColorHoles_FillInDockOrder()
        {
            var level = Level(new[] { "A", "A", "A" }, 3, PictureDisplayMode.FullPicture, 3, new List<GateData>(),
                new List<BlockData> { Block(0, 0, 0, "A", 1), Block(1, 1, 0, "A", 2) });
            var model = new DrainModel(level);
            var s = model.Initial();
            model.LaunchInPlace(s, 1);
            Assert.AreEqual(1, s.Remaining, "hố 2 chỗ ăn 2 ô gần nhất");
            model.LaunchInPlace(s, 0);
            Assert.AreEqual(DrainStatus.Won, s.Status);
        }

        [Test]
        public void Window_AdvancesWhenLowestRowClears()
        {
            var level = Level(new[] { "B", "A" }, 1, PictureDisplayMode.WindowedScroll, 1, new List<GateData>(),
                new List<BlockData> { Block(0, 0, 0, "A", 1), Block(1, 1, 0, "B", 1) });
            var model = new DrainModel(level);
            Assert.AreEqual(DrainStatus.Won, model.Play(new[] { 0, 1 }).Status);
            Assert.AreEqual(DrainStatus.Jammed, model.Play(new[] { 1, 0 }).Status, "B nằm ngoài cửa sổ nên hố B chiếm khay");
        }

        [Test]
        public void Waves_OnlyCurrentWaveIsChoosable()
        {
            var level = Level(new[] { "AB" }, 1, PictureDisplayMode.FullPicture, 2, new List<GateData>(),
                new List<BlockData> { Block(0, 0, 0, "A", 1) }, new List<BlockData> { Block(1, 0, 0, "B", 1) });
            var model = new DrainModel(level);
            var choices = new List<int>();
            model.Choices(model.Initial(), choices);
            CollectionAssert.AreEqual(new[] { 0 }, choices);
        }

        // ───────────── Đối chiếu với GameSimulation ─────────────

        /// <summary>
        /// Mỗi khối thành một đợt riêng ở góc bàn, cạnh cổng trên cùng màu: thứ tự đợt = thứ tự đưa ra.
        /// Nhờ vậy mô phỏng thật chơi được mọi thứ tự mà không phụ thuộc bố trí bàn.
        /// </summary>
        static LevelData Sequential(LevelData src, DrainModel model, IReadOnlyList<int> order)
        {
            var level = new LevelData
            {
                levelId = src.levelId + "-seq", grid = src.grid, displayMode = src.displayMode,
                visibleRows = src.visibleRows, pictureScale = src.pictureScale, dockSlots = src.dockSlots
            };
            foreach (var c in model.Blocks.Select(b => b.color).Distinct()) level.gates.Add(Gate(c, GateSide.Top, 0));
            foreach (int i in order)
            {
                var b = model.Blocks[i];
                level.waves.Add(new WaveData { blocks = { Block(b.id, 0, 0, b.color, b.capacity) } });
            }
            return level;
        }

        static List<int> ShuffledByWave(DrainModel model, System.Random rng) =>
            Enumerable.Range(0, model.BlockCount).OrderBy(i => model.BlockWave[i]).ThenBy(_ => rng.Next()).ToList();

        static (bool won, int launched, int remaining) RunReal(LevelData src, DrainModel model, IReadOnlyList<int> order)
        {
            var level = Sequential(src, model, order);
            var solution = new LevelSolution();
            foreach (int i in order) solution.steps.Add(new SolutionStep { kind = SolutionStepKind.Exit, block = model.Blocks[i].id, door = GateSide.Top });
            var sim = new GameSimulation(level);
            var r = SolutionReplayer.Run(sim, solution, 1 / 30.0);
            int launched = r.Success ? order.Count : r.StepsDone + (r.Status == GameStatus.Jammed ? 1 : 0);
            return (r.Success, launched, sim.Picture.Remaining);
        }

        [TestCaseSource(nameof(LevelPaths))]
        public void Model_MatchesSimulation_OnRandomOrders(string path)
        {
            var so = AssetDatabase.LoadAssetAtPath<LevelSO>(path);
            var model = new DrainModel(so.data);
            var rng = new System.Random(7);
            var orders = new List<List<int>>();
            if (so.solution.HasSteps)
                orders.Add(so.solution.steps.Where(s => s.kind == SolutionStepKind.Exit).Select(s => model.IndexOfBlockId(s.block)).ToList());
            for (int i = 0; i < 20; i++) orders.Add(ShuffledByWave(model, rng));

            var mismatches = new List<string>();
            int wins = 0;
            foreach (var order in orders)
            {
                var fast = model.Play(order);
                var real = RunReal(so.data, model, order);
                bool fastWon = fast.Status == DrainStatus.Won;
                if (fastWon) wins++;
                if (fastWon == real.won && fast.LaunchCount == real.launched && fast.Remaining == real.remaining) continue;
                mismatches.Add($"[{string.Join(",", order)}] mô hình: {fast.Status} sau {fast.LaunchCount} khối, còn {fast.Remaining} ô | " +
                                   $"game: {(real.won ? "Won" : "thua")} sau {real.launched} khối, còn {real.remaining} ô");
            }
            Debug.Log($"[DrainModel] {so.name}: {orders.Count} thứ tự, mô hình thắng {wins}, lệch {mismatches.Count}");
            Assert.IsEmpty(mismatches, string.Join("\n", mismatches));
        }
    }
}
