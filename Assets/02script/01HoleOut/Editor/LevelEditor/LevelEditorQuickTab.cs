using System.Collections.Generic;
using System.IO;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Tab "Bắt đầu nhanh": ba bước cho người mới — chọn tranh, chọn độ khó, bấm một nút để máy dựng màn.
    /// Các tab còn lại giữ cho chỉnh tay chi tiết.
    /// </summary>
    sealed class LevelEditorQuickTab
    {
        static readonly int[] SquareSizes = { 16, 20, 24, 25 };
        const int DefaultImportSize = 20;

        readonly LevelEditorContext _ctx;
        readonly LevelEditorWindow _window;
        readonly LevelEditorJob<LevelDesign.Result> _job = new LevelEditorJob<LevelDesign.Result>();
        int _importSize = DefaultImportSize, _importColors = 5, _seed = 1;
        string _message;
        MessageType _messageType;

        public LevelEditorQuickTab(LevelEditorContext ctx, LevelEditorWindow window)
        {
            _ctx = ctx;
            _window = window;
        }

        public bool Running => _job.Running;
        public bool Poll() => _job.Poll();
        public void Cancel() => _job.Cancel();

        LevelSO Level => _ctx.Level;
        LevelData Data => _ctx.Data;

        public void Draw()
        {
            EditorGUILayout.HelpBox("Làm màn mới trong 3 bước. Các tab khác dùng khi muốn chỉnh tay chi tiết.", MessageType.None);
            DrawPictureStep();
            EditorGUILayout.Space(6);
            DrawTierStep();
            EditorGUILayout.Space(6);
            DrawBuildStep();
            EditorGUILayout.Space(6);
            DrawChecklist();
        }

        // ───────────── Bước 1: Tranh ─────────────

        void DrawPictureStep()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Bước 1 · Tranh", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPreview(140);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        int colors = _ctx.PictureColors().Count;
                        EditorGUILayout.LabelField($"Tranh hiện tại: {Data.Columns}×{Data.Rows} ô, {colors} màu.");
                        EditorGUILayout.LabelField("Tranh vuông 16–25 ô mỗi cạnh trông to và rõ nhất. Tối đa 25×25 và " +
                                                   $"{LevelDesign.MaxColors} màu.", EditorStyles.wordWrappedMiniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Tạo tranh trống:", GUILayout.Width(100));
                            foreach (int size in SquareSizes)
                                if (GUILayout.Button($"{size}×{size}", GUILayout.Width(60))) NewSquare(size);
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Hoặc từ ảnh:", GUILayout.Width(100));
                            _importSize = EditorGUILayout.IntSlider(_importSize, 10, LevelDesign.MaxSide, GUILayout.Width(170));
                            GUILayout.Label("ô,", GUILayout.Width(18));
                            _importColors = EditorGUILayout.IntSlider(_importColors, 2, LevelDesign.MaxColors, GUILayout.Width(150));
                            GUILayout.Label("màu", GUILayout.Width(32));
                            if (GUILayout.Button("Chọn ảnh…", GUILayout.Width(90))) ImportImage();
                        }
                        EditorGUILayout.LabelField("Ảnh được thu nhỏ về số ô đã chọn và đổi sang bảng màu của game. " +
                                                   "Ảnh đơn giản, ít chi tiết, nền một màu cho kết quả đẹp nhất.", EditorStyles.wordWrappedMiniLabel);
                        if (GUILayout.Button("Vẽ / sửa từng ô ở tab Tranh →", GUILayout.Width(220))) _window.ShowTab(LevelEditorWindow.PictureTab);
                    }
                }
            }
        }

        void DrawPreview(float size)
        {
            var r = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            int cols = Data.Columns, rows = Data.Rows;
            if (Event.current.type != EventType.Repaint || cols == 0 || rows == 0) return;
            EditorGUI.DrawRect(r, new Color(0.13f, 0.17f, 0.42f));
            float cs = Mathf.Min(r.width / cols, r.height / rows);
            float ox = r.x + (r.width - cs * cols) / 2, oy = r.y + (r.height - cs * rows) / 2;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                    EditorGUI.DrawRect(new Rect(ox + x * cs, oy + y * cs, Mathf.Max(1, cs - (cs > 4 ? 0.5f : 0)), Mathf.Max(1, cs - (cs > 4 ? 0.5f : 0))),
                        _ctx.ColorOf(Data.grid[y][x].ToString()));
        }

        void NewSquare(int size)
        {
            if (!EditorUtility.DisplayDialog("Tạo tranh trống", $"Thay tranh hiện tại bằng tranh trống {size}×{size}? (Hoàn tác được bằng Ctrl+Z)", "Tạo", "Hủy")) return;
            var row = new string(_ctx.Brush[0], size);
            SetGrid(Enumerable.Repeat(row, size).ToArray(), "Tạo tranh trống");
            _window.ShowTab(LevelEditorWindow.PictureTab);
        }

        void ImportImage()
        {
            string path = EditorUtility.OpenFilePanel("Chọn ảnh", "", "png,jpg,jpeg");
            if (string.IsNullOrEmpty(path)) return;
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Object.DestroyImmediate(tex);
                Message("Không đọc được ảnh này. Thử ảnh PNG hoặc JPG khác.", MessageType.Error);
                return;
            }
            var grid = PictureImport.FromTexture(tex, _importSize, _importColors);
            Object.DestroyImmediate(tex);
            SetGrid(grid, "Nhập tranh từ ảnh");
            Message($"Đã nhập ảnh thành tranh {grid[0].Length}×{grid.Length}. Có thể sửa từng ô ở tab Tranh.", MessageType.Info);
        }

        void SetGrid(string[] grid, string undo)
        {
            _ctx.Record(undo);
            Data.grid = grid;
            Data.displayMode = PictureDisplayMode.FullPicture;
            Data.visibleRows = grid.Length;
            Data.pictureScale = 1;
            _ctx.Changed();
        }

        // ───────────── Bước 2: Độ khó ─────────────

        void DrawTierStep()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Bước 2 · Độ khó", EditorStyles.boldLabel);
                var names = LevelDesign.Presets.Select(p => p.Tier).ToArray();
                int current = System.Array.IndexOf(names, Level.targetTier);
                int picked = GUILayout.Toolbar(Mathf.Max(0, current), names, GUILayout.Height(24));
                if (current < 0 || picked != current)
                {
                    _ctx.Record("Chọn độ khó");
                    Level.targetTier = names[picked];
                    _ctx.Changed();
                }
                EditorGUILayout.LabelField(LevelDesign.Preset(Level.targetTier).Hint, EditorStyles.wordWrappedMiniLabel);
            }
        }

        // ───────────── Bước 3: Dựng ─────────────

        void DrawBuildStep()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Bước 3 · Tạo màn", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Máy tự chia số trên khối, xếp khối và cổng trên bàn, tìm lời giải rồi chơi lại 3 lần để chắc chắn " +
                                           "màn thắng được. Khối, cổng và lời giải cũ sẽ được thay mới.", EditorStyles.wordWrappedMiniLabel);
                var problems = LevelDesign.PictureProblems(Data.grid);
                if (_job.Running)
                {
                    var preset = LevelDesign.Preset(Level.targetTier);
                    double total = preset.Seconds * 2.5 * preset.Attempts;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var r = GUILayoutUtility.GetRect(200, 20, GUILayout.ExpandWidth(true));
                        EditorGUI.ProgressBar(r, Mathf.Clamp01((float)(_job.Elapsed / total)), $"Đang tạo màn {preset.Tier}… {_job.Elapsed:0}s (tối đa khoảng {total / 60:0} phút, máy thử tới {preset.Attempts} cách và giữ cách khó nhất)");
                        if (GUILayout.Button("Dừng", GUILayout.Width(60))) { _job.Cancel(); Message("Đã dừng.", MessageType.Warning); }
                    }
                }
                else
                {
                    foreach (var p in problems) EditorGUILayout.HelpBox(p, MessageType.Error);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(problems.Count > 0))
                            if (GUILayout.Button("Tạo màn tự động", GUILayout.Height(30))) Build();
                        if (GUILayout.Button(new GUIContent("Thử cách khác", "Dựng lại với lựa chọn ngẫu nhiên khác (seed mới)."), GUILayout.Width(110), GUILayout.Height(30)))
                        {
                            _seed += LevelDesign.SeedAttempts;
                            if (problems.Count == 0) Build();
                        }
                    }
                }
                if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);
            }
        }

        void Build()
        {
            var level = Level;
            var preset = LevelDesign.Preset(level.targetTier);
            string id = level.name;
            var grid = (string[])Data.grid.Clone();
            var tiers = _window.Tiers.ToList();
            string tier = level.targetTier;
            int seed = _seed;
            _message = null;
            _job.Start("Đang tạo màn", token => LevelDesign.Run(id, grid, tier, tiers, seed, token),
                r =>
                {
                    if (_ctx.Level != level) return;
                    if (r == null || !r.Ok) { Message(r?.Message ?? "Không dựng được màn.", MessageType.Error); return; }
                    _ctx.Record("Tạo màn tự động");
                    level.data = r.Data;
                    level.solution = r.Solution;
                    level.solution.dataVersion = LevelImporter.DataVersion(r.Data);
                    level.lastReplay = r.Message;
                    level.lastReport = r.Report;
                    level.lastReportVersion = level.solution.dataVersion;
                    _ctx.Changed();
                    AssetDatabase.SaveAssetIfDirty(level);
                    Message(r.Message + " Đã lưu.", r.TierMet ? MessageType.Info : MessageType.Warning);
                },
                ex => Message("Lỗi: " + ex.Message, MessageType.Error));
        }

        // ───────────── Kiểm tra & chiến dịch ─────────────

        void DrawChecklist()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Bước 4 · Kiểm tra và đưa vào game", EditorStyles.boldLabel);
                bool pictureOk = LevelDesign.PictureProblems(Data.grid).Count == 0;
                bool solved = Level.solution != null && Level.solution.HasSteps && Level.solution.dataVersion == _ctx.DataVersion;
                var campaign = CampaignAsset.LoadOrCreate();
                int index = campaign != null ? campaign.levels.IndexOf(Level) : -1;
                Check(pictureOk, "Tranh hợp lệ");
                Check(solved, solved ? "Có lời giải, đã chơi lại thắng" : "Chưa có lời giải khớp tranh hiện tại (bấm Tạo màn tự động)");
                Check(index >= 0, index >= 0 ? $"Đang ở chiến dịch: màn {index + 1}/{campaign.levels.Count}" : "Chưa có trong chiến dịch");
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!solved || campaign == null || index >= 0))
                        if (GUILayout.Button("Thêm vào cuối chiến dịch", GUILayout.Width(180)))
                        {
                            Undo.RecordObject(campaign, "Thêm màn vào chiến dịch");
                            campaign.levels.Add(Level);
                            EditorUtility.SetDirty(campaign);
                            AssetDatabase.SaveAssetIfDirty(campaign);
                        }
                    using (new EditorGUI.DisabledScope(!solved))
                        if (GUILayout.Button("Chơi thử ▶", GUILayout.Width(100))) _window.PlayTest();
                    if (GUILayout.Button("Sắp xếp chiến dịch →", GUILayout.Width(150))) _window.ShowTab(LevelEditorWindow.CampaignTab);
                }
            }
        }

        static void Check(bool ok, string text)
        {
            var style = new GUIStyle(EditorStyles.label) { richText = true };
            GUILayout.Label(ok ? $"<color=#3fbf5f>✔</color>  {text}" : $"<color=#e0802a>○</color>  {text}", style);
        }

        void Message(string text, MessageType type)
        {
            _message = text;
            _messageType = type;
            _ctx.Window.Repaint();
        }
    }
}
