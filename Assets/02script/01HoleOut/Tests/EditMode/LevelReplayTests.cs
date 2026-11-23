using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using NUnit.Framework;
using UnityEditor;

namespace HoleOut.Tests
{
    /// <summary>Kiểm tra bộ mẫu đã nhập: validation và replay lời giải qua API kéo.</summary>
    public class LevelReplayTests
    {
        const string Folder = "Assets/07data/HoleOutTestLevels";

        static IEnumerable<string> LevelPaths() =>
            AssetDatabase.FindAssets("t:LevelSO", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

        static LevelSO Load(string path) => AssetDatabase.LoadAssetAtPath<LevelSO>(path);

        [Test]
        public void SampleSet_IsImported()
        {
            Assert.GreaterOrEqual(LevelPaths().Count(), 1, "Chạy Tools/HoleOut/Import Test Levels trước.");
        }

        [TestCaseSource(nameof(LevelPaths))]
        public void Level_PassesValidation(string path)
        {
            var errors = LevelValidator.Validate(Load(path).data);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [TestCaseSource(nameof(LevelPaths))]
        public void Level_SolutionReplaysToWin(string path)
        {
            var so = Load(path);
            Assert.IsTrue(so.solution.HasSteps, "màn chưa có lời giải");
            Assert.AreEqual(so.data.displayMode, so.solution.verifiedMode, "lời giải kiểm cho chế độ khác");
            var result = SolutionReplayer.Run(so.data, so.solution);
            Assert.IsTrue(result.Success, result.ToString());
        }

        [TestCaseSource(nameof(LevelPaths))]
        public void Level_ReplayStableAcrossSeedsAndTicks(string path)
        {
            var so = Load(path);
            foreach (var (seed, dt) in new[] { (2, 1 / 60.0), (9, 1 / 20.0) })
            {
                var result = SolutionReplayer.Run(so.data, so.solution, dt, seed);
                Assert.IsTrue(result.Success, $"seed {seed}, dt {dt:0.000}: {result}");
            }
        }
    }
}
