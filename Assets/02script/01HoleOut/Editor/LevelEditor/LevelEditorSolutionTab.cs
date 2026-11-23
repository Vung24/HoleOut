using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Lời giải: dựng lời giải từ thứ tự bền, kiểm chứng qua mô phỏng thật, chơi thử trong scene.</summary>
    sealed class LevelEditorSolutionTab
    {
        const string PrefScene = "HoleOut.LevelEditor.PlayScene";
        const string DefaultScene = "Assets/01scene/HoleOut.unity";
        static readonly (int seed, double dt)[] Checks = { (1, 1 / 30.0), (2, 1 / 60.0), (9, 1 / 20.0) };

        readonly LevelEditorContext _ctx;
        readonly LevelEditorJob<(LevelSolution solution, string result)> _job = new LevelEditorJob<(LevelSolution, string)>();
        string _message;
        Vector2 _scroll;

        public LevelEditorSolutionTab(LevelEditorContext ctx) => _ctx = ctx;

        public bool Poll() => _job.Poll();

        public void Cancel() => _job.Cancel();

        public void Draw()
        {
            var so = _ctx.Level;
            var sol = so.solution;
            bool has = sol != null && sol.HasSteps;
            bool matches = has && sol.dataVersion == _ctx.DataVersion;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField(has
                    ? $"Lời giải: {sol.steps.Count} bước ({sol.steps.Count(s => s.kind == SolutionStepKind.Move)} lần dời, {sol.steps.Count(s => s.kind == SolutionStepKind.Exit)} lần ra cổng)"
                    : "Chưa có lời giải.", EditorStyles.boldLabel);
                if (has && !matches)
                    EditorGUILayout.HelpBox("Dữ liệu màn đã đổi sau khi lời giải được kiểm chứng. Dựng lại hoặc kiểm chứng lại.", MessageType.Warning);
                if (!string.IsNullOrEmpty(so.lastReplay)) EditorGUILayout.LabelField("Kiểm chứng gần nhất: " + so.lastReplay, EditorStyles.wordWrappedLabel);

                if (_job.Running)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{_job.Label}… {_job.Elapsed:0.0}s", EditorStyles.boldLabel);
                        if (GUILayout.Button("Hủy", GUILayout.Width(60))) _job.Cancel();
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var report = so.lastReport;
                        bool reportFresh = report != null && report.solvable && so.lastReportVersion == _ctx.DataVersion;
                        using (new EditorGUI.DisabledScope(!reportFresh))
                            if (GUILayout.Button(new GUIContent("Dựng từ phân tích", reportFresh ? "" : "Cần kết quả phân tích mới, có lời giải.")))
                                BuildFromReport(report);
                        using (new EditorGUI.DisabledScope(!has))
                            if (GUILayout.Button("Kiểm chứng")) Verify(sol);
                        if (GUILayout.Button("Chơi thử")) PlayTest();
                    }
                }
                if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorPrefs.GetString(PrefScene, DefaultScene));
                var picked = (SceneAsset)EditorGUILayout.ObjectField("Scene chơi thử", scene, typeof(SceneAsset), false);
                if (picked != scene && picked != null) EditorPrefs.SetString(PrefScene, AssetDatabase.GetAssetPath(picked));
            }

            if (!has) return;
            EditorGUILayout.LabelField("Các bước", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(200));
            var colors = _ctx.AllBlocks().ToDictionary(b => b.id, b => $"{b.color}{b.capacity}");
            for (int i = 0; i < sol.steps.Count; i++)
            {
                var s = sol.steps[i];
                colors.TryGetValue(s.block, out var name);
                string path = string.Join(" → ", s.path.Select(p => p.ToString()));
                EditorGUILayout.LabelField(s.kind == SolutionStepKind.Move
                    ? $"{i + 1}. Dời khối #{s.block} {name}: {path}"
                    : $"{i + 1}. Ra cổng {s.door}: khối #{s.block} {name}{(path.Length > 0 ? " qua " + path : "")}");
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>Dựng lời giải từ thứ tự id khối, kiểm chứng rồi lưu nếu đạt.</summary>
        public void BuildAndVerify(List<int> orderIds)
        {
            var level = _ctx.Level;
            var data = LevelEditorJob<object>.Snapshot(level.data);
            int version = _ctx.DataVersion;
            _message = null;
            _job.Start("Đang dựng và kiểm chứng lời giải", token =>
                {
                    var built = SolutionBuilder.Build(data, orderIds);
                    if (!built.Success) return (null, "Không dựng được: " + built.Error);
                    built.Solution.dataVersion = version;
                    string result = RunChecks(data, built.Solution, token, out bool ok);
                    return (ok ? built.Solution : null, result);
                },
                r =>
                {
                    if (_ctx.Level != level) return;
                    if (r.solution == null) { _message = r.result; return; }
                    _ctx.Record("Dựng lời giải");
                    level.solution = r.solution;
                    level.lastReplay = r.result;
                    _ctx.Changed();
                    _message = "Đã lưu lời giải mới. " + r.result;
                },
                ex => _message = "Lỗi: " + ex.Message);
        }

        void BuildFromReport(DifficultyReport report)
        {
            var flat = _ctx.Data.waves.SelectMany(w => w.blocks).ToList();
            BuildAndVerify(report.referenceOrder.Select(i => flat[i].id).ToList());
        }

        void Verify(LevelSolution solution)
        {
            var level = _ctx.Level;
            var data = LevelEditorJob<object>.Snapshot(level.data);
            var sol = LevelEditorJob<object>.Snapshot(solution);
            int version = _ctx.DataVersion;
            _message = null;
            _job.Start("Đang kiểm chứng", token =>
                {
                    string result = RunChecks(data, sol, token, out bool ok);
                    return (ok ? sol : null, result);
                },
                r =>
                {
                    if (_ctx.Level != level) return;
                    _ctx.Record("Kiểm chứng lời giải");
                    level.lastReplay = r.result;
                    if (r.solution != null)
                    {
                        level.solution.dataVersion = version;
                        level.solution.verifiedMode = level.data.displayMode;
                    }
                    _ctx.Changed();
                    _message = r.result;
                },
                ex => _message = "Lỗi: " + ex.Message);
        }

        /// <summary>Phát lại qua mô phỏng thật với vài seed và bước thời gian khác nhau.</summary>
        static string RunChecks(LevelData data, LevelSolution solution, System.Threading.CancellationToken token, out bool ok)
        {
            ok = true;
            var parts = new List<string>();
            foreach (var (seed, dt) in Checks)
            {
                token.ThrowIfCancellationRequested();
                var r = SolutionReplayer.Run(data, solution, dt, seed);
                parts.Add($"seed {seed}, dt 1/{1 / dt:0}: {r}");
                ok &= r.Success;
            }
            return (ok ? "Đạt. " : "KHÔNG đạt. ") + string.Join("; ", parts);
        }

        public void PlayTest()
        {
            if (EditorApplication.isPlaying) { _message = "Đang ở Play Mode. Dừng trước khi chơi thử."; return; }
            string scene = EditorPrefs.GetString(PrefScene, DefaultScene);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) == null) { _message = $"Không thấy scene {scene}. Chọn scene chơi thử."; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.SaveAssetIfDirty(_ctx.Level);
            SessionState.SetString(GameController.EditorTestLevelKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_ctx.Level)));
            EditorSceneManager.OpenScene(scene);
            EditorApplication.EnterPlaymode();
        }
    }
}
