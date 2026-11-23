using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Chiến dịch: cấp đo được của từng màn theo thứ tự chơi, đo lại hàng loạt.</summary>
    sealed class LevelEditorCampaignTab
    {
        readonly LevelEditorWindow _window;
        readonly LevelEditorJob<DifficultyReport> _job = new LevelEditorJob<DifficultyReport>();
        readonly Queue<LevelSO> _queue = new Queue<LevelSO>();
        int _queueTotal;
        bool _useController = true;
        GameController _controller;
        bool _controllerDirty = true;

        public LevelEditorCampaignTab(LevelEditorWindow window)
        {
            _window = window;
            EditorApplication.hierarchyChanged += MarkControllerDirty;
        }

        public void Dispose()
        {
            Cancel();
            EditorApplication.hierarchyChanged -= MarkControllerDirty;
        }

        void MarkControllerDirty() => _controllerDirty = true;

        public bool Poll()
        {
            bool done = _job.Poll();
            if (!_job.Running && _queue.Count > 0) StartNext();
            return done || _job.Running;
        }

        public void Cancel()
        {
            _queue.Clear();
            _job.Cancel();
        }

        /// <summary>
        /// Thứ tự chơi lấy từ asset chiến dịch mà GameController trong scene đang mở tham chiếu.
        /// Tìm controller bằng FindFirstObjectByType vì cửa sổ editor không có tham chiếu cố định tới scene; kết quả được cache tới khi hierarchy đổi.
        /// </summary>
        List<LevelSO> CampaignOrder(out string source)
        {
            if (_useController)
            {
                if (_controllerDirty) { _controller = Object.FindFirstObjectByType<GameController>(); _controllerDirty = false; }
                var controller = _controller;
                if (controller != null && controller.Campaign != null)
                {
                    var list = controller.Campaign.levels.Where(l => l != null).ToList();
                    source = $"chiến dịch {controller.Campaign.name} của {controller.name} trong scene đang mở";
                    return list;
                }
            }
            source = "mọi asset màn, theo đường dẫn";
            return _window.Levels.ToList();
        }

        public void Draw()
        {
            var levels = CampaignOrder(out var source);
            var tiers = _window.Tiers;
            using (new EditorGUILayout.HorizontalScope())
            {
                _useController = GUILayout.Toggle(_useController, "Theo thứ tự trong scene", GUILayout.Width(170));
                GUILayout.Label("Nguồn: " + source, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (_job.Running || _queue.Count > 0)
                {
                    GUILayout.Label($"Đang đo {_queueTotal - _queue.Count}/{_queueTotal}…");
                    if (GUILayout.Button("Hủy", GUILayout.Width(60))) Cancel();
                }
                else
                {
                    if (GUILayout.Button("Đo lại màn cũ")) Enqueue(levels.Where(IsStale));
                    if (GUILayout.Button("Đo lại tất cả")) Enqueue(levels);
                }
            }
            if (levels.Count == 0) { EditorGUILayout.HelpBox("Chưa có màn nào.", MessageType.Info); return; }

            const float colW = 26, maxH = 140;
            var area = GUILayoutUtility.GetRect(colW * levels.Count + 60, maxH + 40, GUILayout.ExpandWidth(false));
            int steps = Mathf.Max(1, tiers.Count);
            for (int t = 0; t < tiers.Count; t++)
            {
                float y = area.y + maxH - (t + 1) * maxH / steps;
                EditorGUI.DrawRect(new Rect(area.x + 56, y, colW * levels.Count, 1), new Color(1, 1, 1, 0.12f));
                GUI.Label(new Rect(area.x, y - 8, 56, 16), tiers[t].name, EditorStyles.miniLabel);
            }
            var center = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                float x = area.x + 56 + i * colW;
                int measured = l.lastReport != null && l.lastReportVersion != 0 ? DifficultyTier.Measure(tiers, l.lastReport) : -2;
                int target = tiers.ToList().FindIndex(t => t.name == l.targetTier);
                float h = measured < -1 ? 4 : (measured + 1.0f) * maxH / steps;
                var bar = new Rect(x + 3, area.y + maxH - h, colW - 6, h);
                var color = measured < -1 ? new Color(0.5f, 0.5f, 0.5f) : Color.Lerp(new Color(0.3f, 0.75f, 0.35f), new Color(0.9f, 0.3f, 0.25f), (measured + 1f) / steps);
                EditorGUI.DrawRect(bar, color);
                if (IsStale(l)) LevelEditorContext.Outline(bar, new Color(1, 0.8f, 0.2f), 1);
                if (target >= 0)
                {
                    float ty = area.y + maxH - (target + 1f) * maxH / steps;
                    EditorGUI.DrawRect(new Rect(x + 1, ty - 1, colW - 2, 3), Color.white);
                }
                if (l == _window.Selected) LevelEditorContext.Outline(new Rect(x, area.y, colW, maxH + 18), new Color(0.3f, 0.6f, 1f), 1);
                GUI.Label(new Rect(x, area.y + maxH + 2, colW, 16), (i + 1).ToString(), center);
                var hit = new Rect(x, area.y, colW, maxH + 18);
                string tierName = measured >= 0 ? tiers[measured].name : measured == -1 ? "chưa đạt cấp nào" : "chưa đo";
                GUI.Label(hit, new GUIContent("", $"{i + 1}. {(string.IsNullOrEmpty(l.displayName) ? l.name : l.displayName)}\nĐo được: {tierName}{(IsStale(l) ? " (cũ)" : "")}\nMục tiêu: {(target >= 0 ? l.targetTier : "chưa đặt")}"));
                if (Event.current.type == EventType.MouseDown && hit.Contains(Event.current.mousePosition))
                {
                    _window.SelectLevel(l);
                    Event.current.Use();
                }
            }
            EditorGUILayout.LabelField("Cột = cấp đo được, vạch trắng = cấp mục tiêu, viền vàng = kết quả cũ. Bấm cột để mở màn.", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Gợi ý nhịp độ: răng cưa — cứ 4–5 màn thường có 1 màn Khó, sau màn Khó là 1–2 màn dễ thở.", MessageType.None);

            int hardInRow = 0, worst = 0;
            foreach (var l in levels)
            {
                int m = l.lastReport != null && l.lastReportVersion != 0 ? DifficultyTier.Measure(tiers, l.lastReport) : -1;
                hardInRow = m >= 2 ? hardInRow + 1 : 0;
                worst = Mathf.Max(worst, hardInRow);
            }
            if (worst >= 3) EditorGUILayout.HelpBox($"Có {worst} màn Khó trở lên liên tiếp: người chơi dễ bỏ cuộc.", MessageType.Warning);
        }

        static bool IsStale(LevelSO l) => l.lastReport == null || l.lastReportVersion == 0 || l.lastReportVersion != LevelImporter.DataVersion(l.data);

        void Enqueue(IEnumerable<LevelSO> levels)
        {
            foreach (var l in levels.Distinct()) _queue.Enqueue(l);
            _queueTotal = _queue.Count;
            if (!_job.Running && _queue.Count > 0) StartNext();
        }

        void StartNext()
        {
            var level = _queue.Dequeue();
            if (level == null || LevelValidator.Validate(level.data).Count > 0) return;
            var data = LevelEditorJob<DifficultyReport>.Snapshot(level.data);
            var solution = LevelEditorJob<DifficultyReport>.Snapshot(level.solution);
            int version = LevelImporter.DataVersion(level.data);
            _job.Start("Đang đo " + level.name, token => DifficultyAnalyzer.Analyze(data, solution, new DifficultyOptions { Cancel = token }),
                report =>
                {
                    if (level == null) return;
                    level.lastReport = report;
                    level.lastReportVersion = version;
                    EditorUtility.SetDirty(level);
                },
                ex => Debug.LogWarning($"[Level Editor] Đo {level.name} lỗi: {ex.Message}"));
        }
    }
}
