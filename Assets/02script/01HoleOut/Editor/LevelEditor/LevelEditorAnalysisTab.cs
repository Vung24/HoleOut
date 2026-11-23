using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Phân tích: đo độ khó ở luồng nền, so với cấp mục tiêu, dòng thời gian từng bước.</summary>
    sealed class LevelEditorAnalysisTab
    {
        const string PrefAuto = "HoleOut.LevelEditor.AutoAnalyze";
        const double AutoDelay = 1.0;

        readonly LevelEditorContext _ctx;
        readonly LevelEditorWindow _window;
        readonly LevelEditorJob<DifficultyReport> _job = new LevelEditorJob<DifficultyReport>();
        int _seenRevision = -1;
        double _changedAt;
        string _error;
        Vector2 _timelineScroll;

        public LevelEditorAnalysisTab(LevelEditorContext ctx, LevelEditorWindow window)
        {
            _ctx = ctx;
            _window = window;
        }

        bool Auto
        {
            get => EditorPrefs.GetBool(PrefAuto, true);
            set => EditorPrefs.SetBool(PrefAuto, value);
        }

        public bool Running => _job.Running;

        public void Cancel() => _job.Cancel();

        /// <summary>Gọi từ Update của cửa sổ: nhận kết quả và tự chạy lại sau khi dữ liệu ngừng đổi.</summary>
        public void Tick()
        {
            if (_job.Poll()) _window.Repaint();
            if (_job.Running) { _window.Repaint(); return; }
            if (_ctx.Level == null) return;
            if (_seenRevision != _ctx.Revision) { _seenRevision = _ctx.Revision; _changedAt = EditorApplication.timeSinceStartup; _error = null; }
            bool stale = _ctx.Level.lastReportVersion != _ctx.DataVersion;
            if (Auto && stale && _error == null && EditorApplication.timeSinceStartup - _changedAt > AutoDelay
                && LevelValidator.Validate(_ctx.Data).Count == 0)
                Run();
        }

        public void Run()
        {
            var level = _ctx.Level;
            var data = LevelEditorJob<DifficultyReport>.Snapshot(level.data);
            var solution = LevelEditorJob<DifficultyReport>.Snapshot(level.solution);
            int version = _ctx.DataVersion;
            _error = null;
            _job.Start("Đang đo độ khó", token =>
                    DifficultyAnalyzer.Analyze(data, solution, new DifficultyOptions { Cancel = token }),
                report =>
                {
                    if (level == null) return;
                    level.lastReport = report;
                    level.lastReportVersion = version;
                    EditorUtility.SetDirty(level);
                },
                ex => _error = ex.Message);
        }

        public void Draw()
        {
            var so = _ctx.Level;
            DrawToolbar(so);
            if (_error != null) EditorGUILayout.HelpBox("Lỗi khi phân tích: " + _error, MessageType.Error);

            var report = so.lastReport;
            if (report == null || so.lastReportVersion == 0)
            {
                EditorGUILayout.HelpBox("Chưa có kết quả. Bấm Phân tích.", MessageType.Info);
                return;
            }
            if (so.lastReportVersion != _ctx.DataVersion)
                EditorGUILayout.HelpBox("Kết quả dưới đây đo trên dữ liệu cũ; dữ liệu đã đổi từ lần đo trước.", MessageType.Warning);

            DrawTierChecks(report);
            DrawTimeline(report);
            DrawGreedy(report);
            if (!string.IsNullOrEmpty(report.boardError)) EditorGUILayout.HelpBox("Bàn: " + report.boardError, MessageType.Warning);
            foreach (var n in report.notes) EditorGUILayout.HelpBox(n, MessageType.Info);
        }

        void DrawToolbar(LevelSO so)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_job.Running)
                {
                    GUILayout.Label($"{_job.Label}… {_job.Elapsed:0.0}s", EditorStyles.boldLabel);
                    if (GUILayout.Button("Hủy", GUILayout.Width(60))) _job.Cancel();
                }
                else if (GUILayout.Button("Phân tích", GUILayout.Width(100))) Run();
                Auto = GUILayout.Toggle(Auto, "Tự đo lại khi sửa", GUILayout.Width(140));
                GUILayout.FlexibleSpace();

                var tiers = _window.Tiers;
                var names = tiers.Select(t => t.name).ToArray();
                int cur = Mathf.Max(0, System.Array.IndexOf(names, so.targetTier));
                int next = EditorGUILayout.Popup("Cấp mục tiêu", cur, names, GUILayout.Width(260));
                if (names.Length > 0 && names[next] != so.targetTier)
                {
                    _ctx.Record("Đổi cấp mục tiêu");
                    so.targetTier = names[next];
                    _ctx.Changed();
                }
            }
            if (_window.Profile == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox("Đang dùng bộ cấp độ mặc định. Tạo asset để chỉnh ngưỡng trong Inspector.", MessageType.None);
                    if (GUILayout.Button("Tạo bộ cấp độ", GUILayout.Width(110))) CreateProfile();
                }
            }
            else if (GUILayout.Button("Sửa ngưỡng cấp độ", GUILayout.Width(140))) Selection.activeObject = _window.Profile;

            var tier = _window.Tiers.FirstOrDefault(t => t.name == so.targetTier);
            if (tier != null && (so.data.dockSlots != tier.suggestedDockSlots || so.data.waves.Count != tier.suggestedWaves))
                EditorGUILayout.HelpBox($"Cấp {tier.name} gợi ý khay {tier.suggestedDockSlots} chỗ, {tier.suggestedWaves} đợt " +
                                        $"(hiện: khay {so.data.dockSlots}, {so.data.waves.Count} đợt).", MessageType.Info);
        }

        void CreateProfile()
        {
            string path = EditorUtility.SaveFilePanelInProject("Bộ cấp độ khó", "HoleOutDifficultyProfile", "asset", "", "Assets/07data");
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DifficultyProfileSO>(), path);
            AssetDatabase.SaveAssets();
        }

        void DrawTierChecks(DifficultyReport report)
        {
            var tiers = _window.Tiers;
            int measured = DifficultyTier.Measure(tiers, report);
            var target = tiers.FirstOrDefault(t => t.name == _ctx.Level.targetTier) ?? tiers.Last();
            var style = new GUIStyle(EditorStyles.largeLabel) { fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField($"Đo được: {(measured < 0 ? "chưa đạt cấp nào" : tiers[measured].name)}   ·   Mục tiêu: {target.name}", style);

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Thước đo", EditorStyles.boldLabel, GUILayout.Width(180));
                    GUILayout.Label("Đo được", EditorStyles.boldLabel, GUILayout.Width(120));
                    GUILayout.Label($"Mục tiêu ({target.name})", EditorStyles.boldLabel, GUILayout.Width(140));
                }
                foreach (var c in target.Check(report))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var st = new GUIStyle(EditorStyles.label);
                        st.normal.textColor = c.ok ? new Color(0.2f, 0.65f, 0.25f) : new Color(0.9f, 0.25f, 0.2f);
                        GUILayout.Label((c.ok ? "✓ " : "✗ ") + c.metric, st, GUILayout.Width(180));
                        GUILayout.Label(c.measured, GUILayout.Width(120));
                        GUILayout.Label(c.target, GUILayout.Width(140));
                    }
                GUILayout.Label($"Lời giải tham chiếu: {(report.referenceSource == "solution" ? "lời giải trong asset" : "máy tìm")}, " +
                                $"{report.steps.Count} khối, tổng {report.totalUnlockMoves} lần dời.", EditorStyles.miniLabel);
            }
        }

        void DrawTimeline(DifficultyReport report)
        {
            if (report.steps.Count == 0) return;
            EditorGUILayout.LabelField("Từng bước của lời giải", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Đỏ = bước có áp lực (bẫy sai số hoặc mồi); xám = khoảng thở.", EditorStyles.miniLabel);
            const float colW = 52, rowH = 16;
            string[] rowNames = { "Khối", "Bẫy số", "Mồi", "Dời", "Khay trống" };
            _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll, GUILayout.Height(rowH * rowNames.Length + 28));
            var area = GUILayoutUtility.GetRect(70 + colW * report.steps.Count + 8 * 2, rowH * rowNames.Length + 4);
            for (int r = 0; r < rowNames.Length; r++)
                GUI.Label(new Rect(area.x, area.y + r * rowH, 70, rowH), rowNames[r], EditorStyles.miniBoldLabel);
            float x = area.x + 70;
            int wave = report.steps[0].wave;
            var center = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            foreach (var s in report.steps)
            {
                if (s.wave != wave) { EditorGUI.DrawRect(new Rect(x + 2, area.y, 3, rowH * rowNames.Length), Color.white); x += 8; wave = s.wave; }
                bool pressure = s.HasPressure(report.boardAnalyzed);
                EditorGUI.DrawRect(new Rect(x, area.y + rowH, colW - 2, rowH * (rowNames.Length - 1)),
                    pressure ? new Color(0.75f, 0.25f, 0.2f, 0.35f) : new Color(0.5f, 0.5f, 0.5f, 0.2f));
                LevelEditorContext.Swatch(new Rect(x, area.y, colW - 2, rowH - 1), _ctx.ColorOf(s.color), $"{s.color}{s.capacity}");
                GUI.Label(new Rect(x, area.y + rowH, colW - 2, rowH), s.wrongCapacityTraps.ToString(), center);
                GUI.Label(new Rect(x, area.y + rowH * 2, colW - 2, rowH), report.boardAnalyzed ? s.baitBlocks.ToString() : "-", center);
                GUI.Label(new Rect(x, area.y + rowH * 3, colW - 2, rowH), report.boardAnalyzed ? s.unlockMoves.ToString() : "-", center);
                GUI.Label(new Rect(x, area.y + rowH * 4, colW - 2, rowH), s.freeSlotsAfter.ToString(), center);
                x += colW;
            }
            EditorGUILayout.EndScrollView();
        }

        static readonly string[] GreedyNames =
        {
            "Màu lộ nhiều nhất, khối to trước", "Màu lộ nhiều nhất, khối nhỏ trước",
            "Khối to nhất trong màu đang lộ", "Khối nhỏ nhất trong màu đang lộ"
        };

        void DrawGreedy(DifficultyReport report)
        {
            EditorGUILayout.LabelField("Người chơi tham (mong muốn: thua)", EditorStyles.boldLabel);
            foreach (var g in report.greedy)
                EditorGUILayout.LabelField($"{(g.won ? "✗ thắng" : "✓ thua")} — {GreedyNames[(int)g.policy]}" +
                                           (g.won ? "" : $" (kẹt sau {g.launched} khối)"));
        }
    }
}
