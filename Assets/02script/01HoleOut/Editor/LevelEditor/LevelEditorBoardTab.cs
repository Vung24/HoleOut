using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Bàn & Cổng: kéo thả khối trên bàn 6×4 từng đợt, đặt cổng, xem khối nào ra được ngay.</summary>
    sealed class LevelEditorBoardTab
    {
        const float Cell = 64, Margin = 40;
        const int UnlockStateLimit = 4000;
        static readonly string[] ShapeNames = { "1×1", "1×2 (dọc)", "2×1 (ngang)", "2×2", "Chữ L 2×2", "Chữ T 3×2", "Dấu + 3×3" };

        readonly LevelEditorContext _ctx;
        readonly LevelEditorAutoDesign _auto;
        int _wave;
        int _selectedBlock = -1;
        int _selectedGate = -1;
        string _gateColor;
        string _message;

        int _dragGroup = -1;
        Vector2Int _grab;

        int _statusRevision = -1, _statusWave = -1;
        readonly Dictionary<int, string> _status = new Dictionary<int, string>();

        public LevelEditorBoardTab(LevelEditorContext ctx, LevelEditorAutoDesign auto)
        {
            _ctx = ctx;
            _auto = auto;
        }

        LevelData Data => _ctx.Data;

        public void Draw()
        {
            if (Data.waves.Count == 0) { EditorGUILayout.HelpBox("Chưa có đợt nào. Thêm ở tab Số & Đợt.", MessageType.Warning); return; }
            _wave = Mathf.Clamp(_wave, 0, Data.waves.Count - 1);
            var colors = _ctx.PictureColors().Select(k => k.Key).ToArray();
            if (colors.Length > 0 && !colors.Contains(_gateColor)) _gateColor = colors[0];
            if (_selectedGate >= Data.gates.Count) _selectedGate = -1;

            using (new EditorGUILayout.HorizontalScope())
            {
                int w = GUILayout.Toolbar(_wave, Enumerable.Range(1, Data.waves.Count).Select(i => $"Đợt {i}").ToArray(), GUILayout.Width(80 * Data.waves.Count));
                if (w != _wave) { _wave = w; _selectedBlock = -1; }
                GUILayout.Space(20);
                int ci = Mathf.Max(0, System.Array.IndexOf(colors, _gateColor));
                ci = EditorGUILayout.Popup("Màu cổng mới", ci, colors, GUILayout.Width(240));
                if (colors.Length > 0) _gateColor = colors[ci];
                GUILayout.Label("Bấm ngoài mép bàn để đặt cổng.", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var area = GUILayoutUtility.GetRect(DragSolver.BoardWidth * Cell + 2 * Margin, DragSolver.BoardHeight * Cell + 2 * Margin,
                    GUILayout.ExpandWidth(false));
                var origin = new Vector2(area.x + Margin, area.y + Margin);
                RefreshStatus();
                if (Event.current.type == EventType.Repaint) DrawBoard(origin);
                HandleInput(origin);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(300)))
                {
                    DrawSelection(colors);
                    if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Warning);
                    EditorGUILayout.HelpBox("Nhãn dưới khối: \"ra ngay\" = kéo ra cổng không cần dời khối khác; " +
                                            "\"dời N\" = cần dời ít nhất N lần; \"kẹt\" = không ra được trên bàn đợt này.", MessageType.None);
                }
            }
            EditorGUILayout.Space(6);
            _auto.DrawBoard();
        }

        // ───────────── Vẽ ─────────────

        Rect BlockCellRect(Vector2 o, int x, int y) => new Rect(o.x + x * Cell + 3, o.y + y * Cell + 3, Cell - 6, Cell - 6);

        void DrawBoard(Vector2 o)
        {
            var board = new Rect(o.x, o.y, DragSolver.BoardWidth * Cell, DragSolver.BoardHeight * Cell);
            EditorGUI.DrawRect(board, new Color(0.16f, 0.18f, 0.22f));
            for (int x = 0; x < DragSolver.BoardWidth; x++)
                for (int y = 0; y < DragSolver.BoardHeight; y++)
                    EditorGUI.DrawRect(BlockCellRect(o, x, y), new Color(0.22f, 0.25f, 0.3f));

            for (int i = 0; i < Data.gates.Count; i++)
            {
                var g = Data.gates[i];
                var r = GateRect(o, g.side, g.start, g.len);
                LevelEditorContext.Swatch(r, _ctx.ColorOf(g.color), Arrow(g.side), i == _selectedGate);
            }

            foreach (var b in Data.waves[_wave].blocks)
            {
                var c = _ctx.ColorOf(b.color);
                for (int ly = 0; ly < b.h; ly++)
                    for (int lx = 0; lx < b.w; lx++)
                    {
                        if (!b.ContainsCell(lx + 0.5, ly + 0.5)) continue;
                        // Ô của cùng một khối nối liền: nới rộng phần tiếp giáp.
                        var r = BlockCellRect(o, b.gx + lx, b.gy + ly);
                        if (lx + 1 < b.w && b.ContainsCell(lx + 1.5, ly + 0.5)) r.width += 6;
                        if (ly + 1 < b.h && b.ContainsCell(lx + 0.5, ly + 1.5)) r.height += 6;
                        EditorGUI.DrawRect(r, c);
                    }
                var full = new Rect(o.x + b.gx * Cell + 3, o.y + b.gy * Cell + 3, b.w * Cell - 6, b.h * Cell - 6);
                if (b.id == _selectedBlock) LevelEditorContext.Outline(full, Color.white, 3);
                var label = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
                label.normal.textColor = LevelEditorContext.TextOn(c);
                var first = BlockCellRect(o, b.gx, b.gy);
                GUI.Label(new Rect(first.x, first.y + 4, first.width, first.height * 0.6f), $"{(b.mystery ? "?" : "")}{b.color}{b.capacity}{(b.Frozen ? $" [{b.ice}]" : "")}", label);
                var small = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                small.normal.textColor = label.normal.textColor;
                _status.TryGetValue(b.id, out var st);
                GUI.Label(new Rect(first.x, first.y + first.height * 0.55f, first.width, first.height * 0.4f), st, small);
            }
        }

        static string Arrow(GateSide s) => s == GateSide.Top ? "▲" : s == GateSide.Bottom ? "▼" : s == GateSide.Left ? "◀" : "▶";

        static Rect GateRect(Vector2 o, GateSide side, int start, int len)
        {
            float bw = DragSolver.BoardWidth * Cell, bh = DragSolver.BoardHeight * Cell, t = Margin * 0.55f, gap = Margin * 0.12f;
            switch (side)
            {
                case GateSide.Top: return new Rect(o.x + start * Cell + 2, o.y - gap - t, len * Cell - 4, t);
                case GateSide.Bottom: return new Rect(o.x + start * Cell + 2, o.y + bh + gap, len * Cell - 4, t);
                case GateSide.Left: return new Rect(o.x - gap - t, o.y + start * Cell + 2, t, len * Cell - 4);
                default: return new Rect(o.x + bw + gap, o.y + start * Cell + 2, t, len * Cell - 4);
            }
        }

        // ───────────── Trạng thái ra cổng ─────────────

        void RefreshStatus()
        {
            if (_statusRevision == _ctx.Revision && _statusWave == _wave) return;
            _statusRevision = _ctx.Revision;
            _statusWave = _wave;
            _status.Clear();
            var src = Data.waves[_wave].blocks;
            foreach (var b in src)
            {
                if (b.Frozen) { _status[b.id] = $"băng {b.ice}"; continue; }
                var copies = src.Select(x => { var c = x.Copy(); c.mystery = false; return c; }).ToList();
                var target = copies.First(x => x.id == b.id);
                if (BoardReach.CanExit(target, copies, Data.gates)) { _status[b.id] = "ra ngay"; continue; }
                var moves = BoardReach.Unlock(target, copies, Data.gates, UnlockStateLimit);
                _status[b.id] = moves == null ? "kẹt" : $"dời {moves.Count}";
            }
        }

        // ───────────── Thao tác chuột ─────────────

        void HandleInput(Vector2 o)
        {
            var e = Event.current;
            var blocks = Data.waves[_wave].blocks;
            Vector2 local = (e.mousePosition - o) / Cell;
            var cell = new Vector2Int(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            bool onBoard = cell.x >= 0 && cell.y >= 0 && cell.x < DragSolver.BoardWidth && cell.y < DragSolver.BoardHeight;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (onBoard)
                {
                    var hit = blocks.FirstOrDefault(b => b.ContainsCell(local.x - b.gx, local.y - b.gy));
                    _selectedBlock = hit?.id ?? -1;
                    _selectedGate = -1;
                    if (hit != null)
                    {
                        _grab = cell - new Vector2Int(hit.gx, hit.gy);
                        Undo.IncrementCurrentGroup();
                        _dragGroup = Undo.GetCurrentGroup();
                    }
                    e.Use();
                    _ctx.Window.Repaint();
                    return;
                }
                if (TryEdgeClick(o, e.mousePosition)) { e.Use(); _ctx.Window.Repaint(); }
                return;
            }

            if (e.type == EventType.MouseDrag && _dragGroup >= 0)
            {
                var b = blocks.FirstOrDefault(x => x.id == _selectedBlock);
                if (b != null)
                {
                    var t = cell - _grab;
                    t.x = Mathf.Clamp(t.x, 0, DragSolver.BoardWidth - b.w);
                    t.y = Mathf.Clamp(t.y, 0, DragSolver.BoardHeight - b.h);
                    if ((t.x != b.gx || t.y != b.gy) && DragSolver.Free(b, t.x, t.y, blocks))
                    {
                        _ctx.Record("Dời khối");
                        b.gx = t.x; b.gy = t.y;
                        _ctx.Changed();
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && _dragGroup >= 0)
            {
                Undo.CollapseUndoOperations(_dragGroup);
                _dragGroup = -1;
                e.Use();
            }
        }

        /// <summary>Bấm vào vùng ngoài mép: chọn cổng có sẵn, hoặc đặt cổng 1 ô màu đang chọn.</summary>
        bool TryEdgeClick(Vector2 o, Vector2 mouse)
        {
            for (int i = 0; i < Data.gates.Count; i++)
            {
                var g = Data.gates[i];
                if (!GateRect(o, g.side, g.start, g.len).Contains(mouse)) continue;
                _selectedGate = i;
                _selectedBlock = -1;
                return true;
            }
            foreach (GateSide side in System.Enum.GetValues(typeof(GateSide)))
            {
                int limit = side == GateSide.Top || side == GateSide.Bottom ? DragSolver.BoardWidth : DragSolver.BoardHeight;
                for (int p = 0; p < limit; p++)
                {
                    if (!GateRect(o, side, p, 1).Contains(mouse)) continue;
                    if (string.IsNullOrEmpty(_gateColor)) return false;
                    _ctx.Record("Thêm cổng");
                    Data.gates.Add(new GateData { color = _gateColor, side = side, start = p, len = 1 });
                    _selectedGate = Data.gates.Count - 1;
                    _selectedBlock = -1;
                    _ctx.Changed();
                    return true;
                }
            }
            return false;
        }

        // ───────────── Bảng thuộc tính ─────────────

        void DrawSelection(string[] colors)
        {
            var blocks = Data.waves[_wave].blocks;
            var b = blocks.FirstOrDefault(x => x.id == _selectedBlock);
            if (b != null) { DrawBlock(b, blocks, colors); return; }
            if (_selectedGate >= 0) { DrawGate(Data.gates[_selectedGate], colors); return; }
            EditorGUILayout.HelpBox("Bấm một khối để chọn và kéo để dời. Bấm cổng để sửa.", MessageType.Info);
        }

        void DrawBlock(BlockData b, List<BlockData> blocks, string[] colors)
        {
            EditorGUILayout.LabelField($"Khối #{b.id} — đợt {_wave + 1}, ô ({b.gx},{b.gy})", EditorStyles.boldLabel);
            int shape = b.shape == BlockShape.Plus ? 6 : b.shape == BlockShape.T ? 5 : b.shape == BlockShape.L ? 4 : b.w == 2 && b.h == 2 ? 3 : b.w == 2 ? 2 : b.h == 2 ? 1 : 0;
            int newShape = EditorGUILayout.Popup("Hình", shape, ShapeNames);
            int ci = Mathf.Max(0, System.Array.IndexOf(colors, b.color));
            int newColor = EditorGUILayout.Popup("Màu", ci, colors);
            int cap = EditorGUILayout.DelayedIntField("Số", b.capacity);
            bool mystery = EditorGUILayout.Toggle("Bí ẩn", b.mystery);
            int ice = EditorGUILayout.DelayedIntField(new GUIContent("Băng", "Số khối khác phải rời bàn trước khi khối này kéo được. 0 = không băng."), b.ice);

            if (newShape != shape)
            {
                var probe = b.Copy();
                probe.shape = newShape == 6 ? BlockShape.Plus : newShape == 5 ? BlockShape.T : newShape == 4 ? BlockShape.L : BlockShape.Rectangle;
                var frame = BlockData.FrameOf(probe.shape);
                probe.w = frame?.w ?? (newShape == 2 || newShape == 3 ? 2 : 1);
                probe.h = frame?.h ?? (newShape == 1 || newShape == 3 ? 2 : 1);
                if (DragSolver.Free(probe, b.gx, b.gy, blocks))
                {
                    _message = null;
                    _ctx.Record("Đổi hình khối");
                    b.w = probe.w; b.h = probe.h; b.shape = probe.shape;
                    _ctx.Changed();
                }
                else _message = "Không đủ chỗ cho hình mới ở vị trí này. Dời khối trước.";
            }
            if (colors.Length > 0 && colors[newColor] != b.color) { _ctx.Record("Đổi màu khối"); b.color = colors[newColor]; _ctx.Changed(); }
            if (cap != b.capacity && cap > 0) { _ctx.Record("Đổi số khối"); b.capacity = cap; _ctx.Changed(); }
            if (mystery != b.mystery) { _ctx.Record("Đổi khối bí ẩn"); b.mystery = mystery; _ctx.Changed(); }
            ice = Mathf.Clamp(ice, 0, Mathf.Max(0, blocks.Count - 1));
            if (ice != b.ice) { _ctx.Record("Đổi lớp băng"); b.ice = ice; _ctx.Changed(); }

            if (GUILayout.Button("Xóa khối"))
            {
                _ctx.Record("Xóa khối");
                blocks.Remove(b);
                _selectedBlock = -1;
                _ctx.Changed();
                GUIUtility.ExitGUI();
            }
        }

        void DrawGate(GateData g, string[] colors)
        {
            EditorGUILayout.LabelField("Cổng", EditorStyles.boldLabel);
            int ci = Mathf.Max(0, System.Array.IndexOf(colors, g.color));
            EditorGUI.BeginChangeCheck();
            int newColor = EditorGUILayout.Popup("Màu", ci, colors);
            var side = (GateSide)EditorGUILayout.EnumPopup("Cạnh", g.side);
            int limit = side == GateSide.Top || side == GateSide.Bottom ? DragSolver.BoardWidth : DragSolver.BoardHeight;
            int len = EditorGUILayout.IntSlider("Độ dài", g.len, 1, limit);
            int start = EditorGUILayout.IntSlider("Bắt đầu", Mathf.Min(g.start, limit - len), 0, limit - len);
            if (EditorGUI.EndChangeCheck())
            {
                _ctx.Record("Sửa cổng");
                if (colors.Length > 0) g.color = colors[newColor];
                g.side = side; g.len = len; g.start = start;
                _ctx.Changed();
            }
            if (Data.gates.Count(x => x.color == g.color) > 1)
                EditorGUILayout.HelpBox($"Màu {g.color} có nhiều hơn một cổng.", MessageType.Info);
            if (GUILayout.Button("Xóa cổng"))
            {
                _ctx.Record("Xóa cổng");
                Data.gates.Remove(g);
                _selectedGate = -1;
                _ctx.Changed();
                GUIUtility.ExitGUI();
            }
        }
    }
}
