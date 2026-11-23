using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HoleOut.Tests
{
    /// <summary>Dựng lời giải, tự chia số, tự xếp bàn và cấp độ khó.</summary>
    public class LevelDesignToolsTests
    {
        const string Folder = "Assets/07data/HoleOutTestLevels/";

        static LevelData Load(string name) => AssetDatabase.LoadAssetAtPath<LevelSO>(Folder + name + ".asset").data;

        static List<int> OrderIds(LevelData data, DifficultyReport report)
        {
            var flat = data.waves.SelectMany(w => w.blocks).ToList();
            return report.referenceOrder.Select(i => flat[i].id).ToList();
        }

        [TestCase("holeout-c14-waves")]
        [TestCase("holeout-l18-full-25x25")]
        public void SolutionBuilder_FromSearchedOrder_ReplaysToWin(string name)
        {
            var data = Load(name);
            var report = DifficultyAnalyzer.Analyze(data, null);
            Assert.IsTrue(report.solvable, report.Summary());
            var built = SolutionBuilder.Build(data, OrderIds(data, report));
            Assert.IsTrue(built.Success, built.Error);
            var replay = SolutionReplayer.Run(data, built.Solution);
            Assert.IsTrue(replay.Success, replay.ToString());
        }

        [Test]
        public void CapsThenBoardDesigner_ProduceValidReplayableLevel()
        {
            var picture = Load("holeout-c04-long-scroll");
            var caps = CapsDesigner.Run(picture, null, new CapsDesignOptions { Seconds = 3, Waves = 2, MinPerWave = 4, MaxPerWave = 9 });
            Assert.IsNotNull(caps, "không tìm được cách chia số");
            Assert.IsTrue(caps.Report.robust);

            var level = CapsDesigner.NumbersLevel(picture, caps.Blocks, 2);
            var board = BoardDesigner.Run(level, caps.Order, new BoardDesignOptions { Seconds = 3 });
            Assert.IsNotNull(board, "không xếp được bàn");
            level.waves = board.Waves;
            level.gates = board.Gates;
            var errors = LevelValidator.Validate(level);
            Assert.IsEmpty(errors, string.Join("\n", errors));

            var built = SolutionBuilder.Build(level, caps.Order);
            Assert.IsTrue(built.Success, built.Error);
            var replay = SolutionReplayer.Run(level, built.Solution);
            Assert.IsTrue(replay.Success, replay.ToString());
            Debug.Log($"[Thiết kế tự động] c04: {DifficultyAnalyzer.Analyze(level, built.Solution).Summary()}");
        }

        [Test]
        public void Tiers_EasyAlwaysMet_HarderNeedsMore()
        {
            var tiers = DifficultyTier.Defaults();
            var report = DifficultyAnalyzer.Analyze(Load("holeout-c01-small-static"), null);
            Assert.IsTrue(report.solvable);
            int measured = DifficultyTier.Measure(tiers, report);
            Assert.GreaterOrEqual(measured, 0);
            Assert.IsFalse(tiers[tiers.Count - 1].IsMet(report), "màn tutorial không thể đạt cấp khó nhất");
            var unsolvable = new DifficultyReport();
            Assert.AreEqual(-1, DifficultyTier.Measure(tiers, unsolvable));
        }
    }
}
