using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Tranh: vẽ ô màu, đổi cỡ, xem cửa sổ hiện, cảnh báo bố cục và biểu đồ màu theo dải hàng.</summary>
    sealed class LevelEditorPictureTab
    {
        enum Tool { Brush, Fill, Pick }
        static readonly string[] ToolNames = { "Cọ", "Đổ màu", "Hút màu" };

        readonly LevelEditorContext _ctx;
        Tool _tool;
        int _stage;
        int _band;
        int _newColumns, _newRows, _sizeRevision = -1;
        int _strokeGroup = -1;
        bool _advanced;
        Vector2Int? _hover;
        int _warningsRevision = -1;
        List<string> _warnings = new List<string>();

        public LevelEditorPictureTab(LevelEditorContext ctx) => _ctx = ctx;

        LevelData Data => _ctx.Data;

        public void Draw()
        {
            if (_sizeRevision != _ctx.Revision) { _newColumns = Data.Columns; _newRows = Data.Rows; _sizeRevision = _ctx.Revision; }
            DrawSettings();
            DrawTools();
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawGrid();
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(270)))
                {
                    DrawColorList();
                    DrawWarnings();
                    DrawBands();
                }
            }
        }

        // ───────────── Cấu hình tranh ─────────────

        void DrawSettings()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _newColumns = EditorGUILayout.IntField("Cột", _newColumns, GUILayout.Width(220));
                _newRows = EditorGUILayout.IntField("Hàng", _newRows, GUILayout.Width(220));
                using (new EditorGUI.DisabledScope(_newColumns == Data.Columns && _newRows == Data.Rows))
                    if (GUILayout.Button("Đổi cỡ", GUILayout.Width(70)))
                        Resize(Mathf.Clamp(_newColumns, 1, LevelDesign.MaxSide), Mathf.Clamp(_newRows, 1, LevelDesign.MaxSide));
                GUILayout.Label($"Tối đa {LevelDesign.MaxSide}×{LevelDesign.MaxSide}. Thêm/bớt hàng ở phía xa, cột ở bên phải; ô mới tô màu đang chọn.", EditorStyles.miniLabel);
            }
            // Game hiện luôn hiện trọn tranh; các tùy chọn trượt chỉ còn cho dữ liệu cũ.
            _advanced = EditorGUILayout.Foldout(_advanced || Data.displayMode != PictureDisplayMode.FullPicture || Data.pictureScale != 1,
                "Nâng cao (tranh trượt, tỉ lệ ô — game hiện không dùng)", true);
            if (!_advanced) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var mode = (PictureDisplayMode)EditorGUILayout.EnumPopup("Hiển thị", Data.displayMode, GUILayout.Width(300));
                int vis = Data.visibleRows;
                using (new EditorGUI.DisabledScope(mode != PictureDisplayMode.WindowedScroll))
                    vis = EditorGUILayout.IntField("Số hàng hiện", Data.visibleRows, GUILayout.Width(220));
                float scale = EditorGUILayout.FloatField("Tỉ lệ tranh", Data.pictureScale, GUILayout.Width(220));
                if (EditorGUI.EndChangeCheck())
                {
                    _ctx.Record("Đổi cấu hình tranh");
                    Data.displayMode = mode;
                    Data.visibleRows = Mathf.Max(1, vis);
                    Data.pictureScale = Mathf.Max(0.05f, scale);
                    _ctx.Changed();
                }
            }
        }

        void DrawTools()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _tool = (Tool)GUILayout.Toolbar((int)_tool, ToolNames, GUILayout.Width(220));
                GUILayout.Label("Alt+bấm: hút màu · chuột phải: thao tác hàng/cột", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Lật ngang")) Edit("Lật ngang tranh", g => g.Select(r => new string(r.Reverse().ToArray())).ToArray());
                if (GUILayout.Button("Chép dạng chữ")) EditorGUIUtility.systemCopyBuffer = string.Join("\n", Data.grid);
                if (GUILayout.Button("Dán dạng chữ")) Paste();
            }

            // Bảng màu chuẩn: một ô cho mỗi mã.
            var r = GUILayoutUtility.GetRect(10, 24, GUILayout.ExpandWidth(true));
            float w = Mathf.Min(30, r.width / PaletteSO.Standard.Length);
            var seen = new HashSet<string>();
            int i = 0;
            foreach (var s in PaletteSO.Standard)
            {
                if (!seen.Add(s.code)) continue;
                var cell = new Rect(r.x + i++ * w, r.y, w - 2, r.height);
                LevelEditorContext.Swatch(cell, _ctx.ColorOf(s.code), s.code, s.code == _ctx.Brush);
                GUI.Label(cell, new GUIContent("", $"{s.code} — {s.name}"));
                if (Event.current.type == EventType.MouseDown && cell.Contains(Event.current.mousePosition))
                {
                    _ctx.Brush = s.code;
                    Event.current.Use();
                    _ctx.Window.Repaint();
                }
            }
        }

        // ───────────── Lưới vẽ ─────────────

        void DrawGrid()
        {
            int cols = Data.Columns, rows = Data.Rows;
            if (cols == 0 || rows == 0) { EditorGUILayout.HelpBox("Tranh trống.", MessageType.Warning); return; }

            float avail = Mathf.Max(200, _ctx.Window.position.width - 230 - 290);
            float cs = Mathf.Clamp(Mathf.Floor(Mathf.Min(avail / cols, 640f / rows)), 5, 30);
            using (new EditorGUILayout.VerticalScope())
            {
                bool windowed = Data.displayMode == PictureDisplayMode.WindowedScroll;
                int vis = Data.EffectiveVisibleRows;
                if (windowed && rows > vis)
                    _stage = EditorGUILayout.IntSlider("Giai đoạn trượt", _stage, 0, rows - vis, GUILayout.Width(Mathf.Max(260, cols * cs)));
                else _stage = 0;

                var area = GUILayoutUtility.GetRect(cols * cs, rows * cs, GUILayout.ExpandWidth(false));
                var e = Event.current;
                if (e.type == EventType.Repaint)
                {
                    for (int r = 0; r < rows; r++)
                    {
                        string row = Data.grid[r];
                        int wIdx = rows - 1 - r;
                        bool inWindow = !windowed || (wIdx >= _stage && wIdx < _stage + vis);
                        for (int c = 0; c < cols; c++)
                        {
                            var cell = new Rect(area.x + c * cs, area.y + r * cs, cs - (cs > 8 ? 1 : 0), cs - (cs > 8 ? 1 : 0));
                            EditorGUI.DrawRect(cell, _ctx.ColorOf(c < row.Length ? row[c].ToString() : "?"));
                            if (!inWindow) EditorGUI.DrawRect(cell, new Color(0, 0, 0, 0.55f));
                        }
                    }
                    if (windowed)
                    {
                        float top = area.y + (rows - _stage - vis) * cs, bottom = area.y + (rows - _stage) * cs;
                        LevelEditorContext.Outline(new Rect(area.x - 2, Mathf.Max(area.y, top) - 2, cols * cs + 4, bottom - Mathf.Max(area.y, top) + 4), Color.white);
                    }
                    if (_hover.HasValue)
                        LevelEditorContext.Outline(new Rect(area.x + _hover.Value.x * cs, area.y + _hover.Value.y * cs, cs, cs), Color.white, 1);
                }
                GUILayout.Label("▼ mép thoát (hàng 0 gần khay nhất)", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(Mathf.Max(200, cols * cs)));
                if (_hover.HasValue)
                {
                    var h = _hover.Value;
                    GUILayout.Label($"Cột {h.x}, hàng {rows - 1 - h.y} tính từ mép thoát — màu {Data.grid[h.y][h.x]}", EditorStyles.miniLabel);
                }
                HandleGridInput(area, cs);
            }
        }

        void HandleGridInput(Rect area, float cs)
        {
            var e = Event.current;
            Vector2Int? cell = null;
            if (area.Contains(e.mousePosition))
                cell = new Vector2Int(Mathf.Clamp((int)((e.mousePosition.x - area.x) / cs), 0, Data.Columns - 1),
                                      Mathf.Clamp((int)((e.mousePosition.y - area.y) / cs), 0, Data.Rows - 1));
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                if (cell != _hover) { _hover = cell; _ctx.Window.Repaint(); }
            }
            if (!cell.HasValue) return;
            var p = cell.Value;

            if (e.type == EventType.MouseDown && e.button == 1)
            {
                RowMenu(p);
                e.Use();
                return;
            }
            if (e.button != 0) return;

            if (e.type == EventType.MouseDown)
            {
                if (e.alt || _tool == Tool.Pick) { _ctx.Brush = Data.grid[p.y][p.x].ToString(); e.Use(); _ctx.Window.Repaint(); return; }
                if (_tool == Tool.Fill) { Fill(p); e.Use(); return; }
                Undo.IncrementCurrentGroup();
                _strokeGroup = Undo.GetCurrentGroup();
                Paint(p);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _strokeGroup >= 0)
            {
                Paint(p);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _strokeGroup >= 0)
            {
                Undo.CollapseUndoOperations(_strokeGroup);
                Undo.SetCurrentGroupName("Vẽ tranh");
                _strokeGroup = -1;
                e.Use();
            }
        }

        void Paint(Vector2Int p)
        {
            string row = Data.grid[p.y];
            if (row[p.x].ToString() == _ctx.Brush) return;
            _ctx.Record("Vẽ tranh");
            var chars = row.ToCharArray();
            chars[p.x] = _ctx.Brush[0];
            Data.grid[p.y] = new string(chars);
            _ctx.Changed();
        }

        void Fill(Vector2Int start)
        {
            char from = Data.grid[start.y][start.x], to = _ctx.Brush[0];
            if (from == to) return;
            Edit("Đổ màu", g =>
            {
                var cells = g.Select(r => r.ToCharArray()).ToArray();
                var q = new Queue<Vector2Int>();
                q.Enqueue(start);
                cells[start.y][start.x] = to;
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = c + d;
                        if (n.y < 0 || n.y >= cells.Length || n.x < 0 || n.x >= cells[n.y].Length || cells[n.y][n.x] != from) continue;
                        cells[n.y][n.x] = to;
                        q.Enqueue(n);
                    }
                }
                return cells.Select(r => new string(r)).ToArray();
            });
        }

        void RowMenu(Vector2Int p)
        {
            var menu = new GenericMenu();
            int r = p.y, c = p.x;
            char brush = _ctx.Brush[0];
            menu.AddItem(new GUIContent("Chèn hàng phía xa"), false, () => Edit("Chèn hàng", g => g.Take(r).Append(new string(brush, Data.Columns)).Concat(g.Skip(r)).ToArray()));
            menu.AddItem(new GUIContent("Chèn hàng phía gần"), false, () => Edit("Chèn hàng", g => g.Take(r + 1).Append(new string(brush, Data.Columns)).Concat(g.Skip(r + 1)).ToArray()));
            menu.AddItem(new GUIContent("Nhân đôi hàng"), false, () => Edit("Nhân đôi hàng", g => g.Take(r + 1).Append(g[r]).Concat(g.Skip(r + 1)).ToArray()));
            if (Data.Rows > 1)
                menu.AddItem(new GUIContent("Xóa hàng"), false, () => Edit("Xóa hàng", g => g.Where((_, i) => i != r).ToArray()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Tô cả hàng"), false, () => Edit("Tô hàng", g => g.Select((row, i) => i == r ? new string(brush, row.Length) : row).ToArray()));
            menu.AddItem(new GUIContent("Tô cả cột"), false, () => Edit("Tô cột", g => g.Select(row => { var ch = row.ToCharArray(); ch[c] = brush; return new string(ch); }).ToArray()));
            menu.AddItem(new GUIContent("Chèn cột bên trái"), false, () => Edit("Chèn cột", g => g.Select(row => row.Insert(c, brush.ToString())).ToArray()));
            if (Data.Columns > 1)
                menu.AddItem(new GUIContent("Xóa cột"), false, () => Edit("Xóa cột", g => g.Select(row => row.Remove(c, 1)).ToArray()));
            menu.ShowAsContext();
        }

        void Edit(string undoName, System.Func<string[], string[]> change)
        {
            var next = change(Data.grid);
            if (next == null || next.Length == 0) return;
            _ctx.Record(undoName);
            Data.grid = next;
            _ctx.Changed();
        }

        void Resize(int cols, int rows)
        {
            char brush = _ctx.Brush[0];
            Edit("Đổi cỡ tranh", g =>
            {
                var list = g.Select(r => r.Length >= cols ? r.Substring(0, cols) : r + new string(brush, cols - r.Length)).ToList();
                while (list.Count < rows) list.Insert(0, new string(brush, cols));
                if (list.Count > rows) list.RemoveRange(0, list.Count - rows);
                return list.ToArray();
            });
        }

        void Paste()
        {
            var lines = (EditorGUIUtility.systemCopyBuffer ?? "").Split('\n')
                .Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
            if (lines.Length == 0 || lines.Any(l => l.Length != lines[0].Length))
            {
                EditorUtility.DisplayDialog("Dán tranh", "Bộ nhớ tạm cần các dòng chữ cùng độ dài, mỗi chữ là một mã màu. Dòng đầu là hàng xa nhất.", "OK");
                return;
            }
            Edit("Dán tranh", _ => lines);
        }

        // ───────────── Bảng bên phải ─────────────

        void DrawColorList()
        {
            EditorGUILayout.LabelField("Màu trong tranh", EditorStyles.boldLabel);
            int total = Mathf.Max(1, Data.Rows * Data.Columns);
            foreach (var kv in _ctx.PictureColors())
            {
                var r = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
                LevelEditorContext.Swatch(new Rect(r.x, r.y + 1, 24, 16), _ctx.ColorOf(kv.Key), kv.Key, kv.Key == _ctx.Brush);
                string name = PaletteSO.TryGetSwatch(kv.Key, out var s) ? s.name : "?";
                GUI.Label(new Rect(r.x + 30, r.y, r.width - 30, r.height), $"{name}: {kv.Value} ô ({100f * kv.Value / total:0}%)");
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    _ctx.Brush = kv.Key;
                    Event.current.Use();
                    _ctx.Window.Repaint();
                }
            }
        }

        void DrawWarnings()
        {
            if (_warningsRevision != _ctx.Revision) { _warnings = Warnings(); _warningsRevision = _ctx.Revision; }
            if (_warnings.Count == 0) return;
            EditorGUILayout.Space(4);
            foreach (var w in _warnings) EditorGUILayout.HelpBox(w, w.StartsWith("Tốt") ? MessageType.Info : MessageType.Warning);
        }

        List<string> Warnings()
        {
            var list = new List<string>();
            var g = Data.grid;
            if (g == null || g.Length == 0) return list;
            int rows = g.Length, cols = g[0].Length;

            string near = g[rows - 1];
            if (near.Distinct().Count() == 1)
                list.Add($"Hàng sát mép thoát chỉ có màu {near[0]}: nước đầu dễ đoán. Nên trộn 2–3 màu.");

            int edgeLimit = Mathf.Max(4, Data.EffectiveVisibleRows / 2);
            foreach (var (c, side) in new[] { (0, "trái"), (cols - 1, "phải") })
            {
                int best = 0, run = 0;
                char bestColor = ' ';
                for (int r = 0; r < rows; r++)
                {
                    run = r > 0 && g[r][c] == g[r - 1][c] ? run + 1 : 1;
                    if (run > best) { best = run; bestColor = g[r][c]; }
                }
                if (best >= edgeLimit)
                    list.Add($"Cột mép {side} có dải {bestColor} dài {best} hàng: mở lối đi thẳng, dễ phá lớp khóa.");
                if (cols == 1) break;
            }

            int colors = g.SelectMany(r => r).Distinct().Count();
            if (colors > 8) list.Add($"{colors} màu: máy khó tìm lời giải. Nên giữ 7–8 màu.");

            int bands = g.Count(r => r.Distinct().Count() == 1);
            list.Add(bands > 0
                ? $"Tốt: {bands} hàng một màu chạy hết bề ngang (lớp khóa, buộc mở nhiều hố cùng lúc)."
                : "Chưa có lớp khóa: thử thêm dải màu chạy hết bề ngang.");
            return list;
        }

        void DrawBands()
        {
            EditorGUILayout.Space(6);
            if (_band <= 0) _band = Data.displayMode == PictureDisplayMode.WindowedScroll ? Data.EffectiveVisibleRows : 5;
            _band = Mathf.Max(1, EditorGUILayout.IntField("Màu theo dải hàng", _band));
            EditorGUILayout.LabelField("Từ mép thoát đi xa; dùng để chia số khối theo giai đoạn.", EditorStyles.miniLabel);
            var g = Data.grid;
            int rows = g.Length;
            for (int start = 0; start < rows; start += _band)
            {
                int end = Mathf.Min(rows, start + _band);
                var counts = new Dictionary<char, int>();
                for (int wIdx = start; wIdx < end; wIdx++)
                    foreach (char ch in g[rows - 1 - wIdx]) counts[ch] = counts.TryGetValue(ch, out var n) ? n + 1 : 1;
                int total = counts.Values.Sum();
                var r = GUILayoutUtility.GetRect(10, 18, GUILayout.ExpandWidth(true));
                GUI.Label(new Rect(r.x, r.y, 52, r.height), $"{start}–{end - 1}", EditorStyles.miniLabel);
                var bar = new Rect(r.x + 54, r.y + 1, r.width - 56, r.height - 2);
                float x = bar.x;
                var tip = new StringBuilder();
                foreach (var kv in counts.OrderByDescending(k => k.Value))
                {
                    float wPx = bar.width * kv.Value / total;
                    var seg = new Rect(x, bar.y, wPx, bar.height);
                    LevelEditorContext.Swatch(seg, _ctx.ColorOf(kv.Key.ToString()), wPx > 22 ? kv.Value.ToString() : null);
                    x += wPx;
                    tip.Append($"{kv.Key}: {kv.Value}  ");
                }
                GUI.Label(bar, new GUIContent("", tip.ToString()));
            }
        }
    }
}
