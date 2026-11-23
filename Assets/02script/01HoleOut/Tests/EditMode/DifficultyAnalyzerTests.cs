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
    public class DifficultyAnalyzerTests
    {
        const string Folder = "Assets/07data/HoleOutTestLevels";

        static IEnumerable<string> LevelPaths() =>
            AssetDatabase.FindAssets("t:LevelSO", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p);

        [Test]
        public void BoardReach_DirectExit_AndOneMoveUnlock()
        {
            var gates = new List<GateData> { Gate("A", GateSide.Top, 0) };
            var a = Block(0, 0, 1, "A", 1);
            var blocker = Block(1, 0, 0, "B", 1, w: 2);
            var board = new List<BlockData> { a, blocker };
            Assert.IsFalse(BoardReach.CanExit(a, board, gates));
            var moves = BoardReach.Unlock(a, board, gates, 1000);
            Assert.IsNotNull(moves);
            Assert.AreEqual(1, moves.Count);
            Assert.AreEqual(1, moves[0].Block);
            Assert.IsTrue(BoardReach.CanExit(a, board, gates));
        }

        [Test]
        public void TightDock_HasLosingChoices()
        {
            // Hàng gần: A B. Hàng xa: B A. Khay 1 chỗ: chỉ màu đang lộ mới lấy được.
            var level = Level(new[] { "BA", "AB" }, 2, PictureDisplayMode.FullPicture, 1,
                new List<GateData> { Gate("A", GateSide.Top, 0), Gate("B", GateSide.Top, 3) },
                new List<BlockData> { Block(0, 0, 0, "A", 1), Block(1, 3, 0, "B", 2), Block(2, 1, 2, "A", 1) });
            var report = DifficultyAnalyzer.Analyze(level);
            Assert.IsTrue(report.solvable, report.Summary());
            Assert.IsTrue(report.boardAnalyzed, report.Summary());
            Assert.Greater(report.steps.Sum(s => s.losingChoices), 0, report.Summary());
        }

        [TestCaseSource(nameof(LevelPaths))]
        public void TestLevel_IsSolvableAndReported(string path)
        {
            var so = AssetDatabase.LoadAssetAtPath<LevelSO>(path);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var report = DifficultyAnalyzer.Analyze(so.data, so.solution);
            Debug.Log($"[Độ khó] {so.name} ({watch.ElapsedMilliseconds} ms)\n{report.Summary()}");
            Assert.IsTrue(report.solvable, report.Summary());
            Assert.AreEqual(so.data.waves.Sum(w => w.blocks.Count), report.steps.Count);
            Assert.AreEqual(4, report.greedy.Count);
        }
    }
}
