using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Tab Số & Đợt: số trên từng khối theo màu, gán đợt, khay, kiểm tra tổng và số quá gần nhau.</summary>
    sealed class LevelEditorNumbersTab
    {
        /// <summary>Hai khối cùng màu, cùng đợt lệch ít hơn mức này thì lấy khối nào cũng như nhau.</summary>
        public const int MinCapacityGap = 3;

        readonly LevelEditorContext _ctx;
        readonly LevelEditorAutoDesign _auto;
        int _addWave;
        string _message;

        public LevelEditorNumbersTab(LevelEditorContext ctx, LevelEditorAutoDesign auto)
        {
            _ctx = ctx;
            _auto = auto;
        }

        LevelData Data => _ctx.Data;

        public void Draw()
        {
            DrawHeader();
            EditorGUILayout.Space(6);
            DrawColorRows();
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Warning);
            DrawWarnings();
            EditorGUILayout.Space(6);
            _auto.DrawCaps();
        }

        void DrawHeader()
        {
            EditorGUI.BeginChangeCheck();
            int slots = EditorGUILayout.IntSlider("Chỗ trên khay", Data.dockSlots, 1, Dock.MaxSlotsLimit);
            if (EditorGUI.EndChangeCheck()) { _ctx.Record("Đổi số chỗ khay"); Data.dockSlots = slots; _ctx.Changed(); }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Số đợt: {Data.waves.Count}", GUILayout.Width(150));
                if (GUILayout.Button("+ Đợt", GUILayout.Width(70)))
                {
                    _ctx.Record("Thêm đợt");
                    Data.waves.Add(new WaveData());
                    _ctx.Changed();
                }
                var last = Data.waves.LastOrDefault();
                using (new EditorGUI.DisabledScope(Data.waves.Count <= 1 || last == null || last.blocks.Count > 0))
                    if (GUILayout.Button("− Đợt cuối", GUILayout.Width(90)))
                    {
                        _ctx.Record("Xóa đợt");
                        Data.waves.RemoveAt(Data.waves.Count - 1);
                        _ctx.Changed();
                    }
                GUILayout.Label("Đợt sau chỉ hiện khi bàn hết khối của đợt trước. Chỉ xóa được đợt cuối khi đợt đó trống.", EditorStyles.miniLabel);
            }
            _addWave = Mathf.Clamp(_addWave, 0, Data.waves.Count - 1);
            _addWave = EditorGUILayout.Popup("Khối mới vào", _addWave, WaveNames());
        }

        string[] WaveNames() => Enumerable.Range(1, Data.waves.Count).Select(i => $"Đợt {i}").ToArray();

        void DrawColorRows()
        {
            var picture = _ctx.PictureColors().ToDictionary(k => k.Key, k => k.Value);
            var colors = picture.Keys.ToList();
            foreach (var b in _ctx.AllBlocks()) if (!colors.Contains(b.color)) colors.Add(b.color);
            var waveNames = WaveNames();

            foreach (var color in colors)
            {
                picture.TryGetValue(color, out int need);
                var blocks = new List<(int wave, BlockData block)>();
                for (int wi = 0; wi < Data.waves.Count; wi++)
                    foreach (var b in Data.waves[wi].blocks) if (b.color == color) blocks.Add((wi, b));
                int sum = blocks.Sum(x => x.block.capacity);

                using (new EditorGUILayout.HorizontalScope("box"))
                {
                    var sw = GUILayoutUtility.GetRect(28, 22, GUILayout.Width(28));
                    LevelEditorContext.Swatch(sw, _ctx.ColorOf(color), color);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(110)))
                    {
                        GUILayout.Label($"Tranh: {need}", EditorStyles.miniLabel);
                        var st = new GUIStyle(EditorStyles.miniBoldLabel);
                        st.normal.textColor = sum == need ? new Color(0.2f, 0.65f, 0.25f) : Color.red;
                        GUILayout.Label(sum == need ? $"Khối: {sum} ✓" : $"Khối: {sum} ({sum - need:+0;-0})", st);
                    }

                    foreach (var (wave, b) in blocks.OrderBy(x => x.wave).ThenByDescending(x => x.block.capacity))
                        DrawChip(wave, b, waveNames);

                    if (GUILayout.Button("+", GUILayout.Width(24), GUILayout.Height(20))) AddBlock(color, Mathf.Max(1, need - sum));
                    GUILayout.FlexibleSpace();
                }
            }
        }

        void DrawChip(int wave, BlockData b, string[] waveNames)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Width(150)))
            {
                int newWave = EditorGUILayout.Popup(wave, waveNames, GUILayout.Width(56));
                int cap = EditorGUILayout.DelayedIntField(b.capacity, GUILayout.Width(40));
                GUILayout.Label($"{b.w}×{b.h}", EditorStyles.miniLabel, GUILayout.Width(26));
                bool delete = GUILayout.Button("×", GUILayout.Width(18));

                if (cap != b.capacity && cap > 0) { _ctx.Record("Đổi số khối"); b.capacity = cap; _ctx.Changed(); }
                if (newWave != wave) MoveToWave(b, wave, newWave);
                if (delete)
                {
                    _ctx.Record("Xóa khối");
                    Data.waves[wave].blocks.Remove(b);
                    _ctx.Changed();
                    GUIUtility.ExitGUI();
                }
            }
        }

        void AddBlock(string color, int capacity)
        {
            var wave = Data.waves[_addWave];
            var b = new BlockData { id = _ctx.NextBlockId(), w = 1, h = 1, color = color, capacity = capacity };
            var spot = _ctx.FreeSpot(wave, b);
            if (!spot.HasValue) { _message = $"Bàn đợt {_addWave + 1} đã kín, không còn chỗ cho khối 1×1."; return; }
            _message = null;
            _ctx.Record("Thêm khối");
            b.gx = spot.Value.x; b.gy = spot.Value.y;
            wave.blocks.Add(b);
            _ctx.Changed();
        }

        void MoveToWave(BlockData b, int from, int to)
        {
            var target = Data.waves[to];
            Vector2Int? spot = DragSolver.Free(b, b.gx, b.gy, target.blocks) ? new Vector2Int(b.gx, b.gy) : _ctx.FreeSpot(target, b);
            if (!spot.HasValue) { _message = $"Bàn đợt {to + 1} không còn chỗ cho khối {b.w}×{b.h}."; return; }
            _message = null;
            _ctx.Record("Chuyển đợt");
            Data.waves[from].blocks.Remove(b);
            b.gx = spot.Value.x; b.gy = spot.Value.y;
            target.blocks.Add(b);
            _ctx.Changed();
            GUIUtility.ExitGUI();
        }

        void DrawWarnings()
        {
            var list = new List<string>();
            for (int wi = 0; wi < Data.waves.Count; wi++)
            {
                var bs = Data.waves[wi].blocks;
                for (int i = 0; i < bs.Count; i++)
                    for (int j = i + 1; j < bs.Count; j++)
                        if (bs[i].color == bs[j].color && Mathf.Abs(bs[i].capacity - bs[j].capacity) < MinCapacityGap)
                            list.Add($"Đợt {wi + 1}: {bs[i].color}{bs[i].capacity} và {bs[j].color}{bs[j].capacity} lệch dưới {MinCapacityGap}: lấy khối nào cũng như nhau, không tạo được bẫy.");
                if (bs.Count == 0) list.Add($"Đợt {wi + 1} chưa có khối.");
            }
            foreach (var color in _ctx.AllBlocks().Select(b => b.color).Distinct())
                if (!Data.gates.Any(g => g.color == color))
                    list.Add($"Màu {color} chưa có cổng: thêm ở tab Bàn & Cổng.");
            foreach (var w in list) EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
    }
}
