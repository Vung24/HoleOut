using System.Collections.Generic;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Trạng thái chung của cửa sổ Level Editor: màn đang sửa, màu đang chọn, ghi Undo.</summary>
    sealed class LevelEditorContext
    {
        public LevelSO Level;
        public EditorWindow Window;
        public string Brush = "L";
        /// <summary>Tăng mỗi lần dữ liệu màn đổi (kể cả Undo); dùng làm khóa cache.</summary>
        public int Revision;

        int _versionRevision = -1, _version;

        public LevelData Data => Level.data;

        /// <summary>Gọi trước khi sửa dữ liệu để Undo được.</summary>
        public void Record(string undoName) => Undo.RecordObject(Level, undoName);

        /// <summary>Gọi sau khi sửa dữ liệu.</summary>
        public void Changed()
        {
            EditorUtility.SetDirty(Level);
            Revision++;
            Window.Repaint();
        }

        public int DataVersion
        {
            get
            {
                if (_versionRevision != Revision) { _version = LevelImporter.DataVersion(Data); _versionRevision = Revision; }
                return _version;
            }
        }

        public Color ColorOf(string code)
        {
            if (Level != null && Level.visuals != null)
            {
                var v = Level.visuals.Get(code);
                if (v != null) return v.color;
            }
            return PaletteSO.Default(code);
        }

        /// <summary>Màu chữ đọc được trên nền màu đó.</summary>
        public static Color TextOn(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f > 0.6f ? Color.black : Color.white;

        /// <summary>Số ô từng màu trong tranh, theo thứ tự bảng màu chuẩn.</summary>
        public List<KeyValuePair<string, int>> PictureColors()
        {
            var counts = new Dictionary<string, int>();
            if (Data.grid != null)
                foreach (var row in Data.grid)
                    foreach (char ch in row)
                    {
                        string k = ch.ToString();
                        counts[k] = counts.TryGetValue(k, out var n) ? n + 1 : 1;
                    }
            var list = new List<KeyValuePair<string, int>>();
            foreach (var s in PaletteSO.Standard)
                if (counts.TryGetValue(s.code, out var n)) { list.Add(new KeyValuePair<string, int>(s.code, n)); counts.Remove(s.code); }
            foreach (var kv in counts) list.Add(kv);
            return list;
        }

        public IEnumerable<BlockData> AllBlocks()
        {
            foreach (var w in Data.waves) foreach (var b in w.blocks) yield return b;
        }

        public int NextBlockId()
        {
            int id = 0;
            foreach (var b in AllBlocks()) id = Mathf.Max(id, b.id + 1);
            return id;
        }

        /// <summary>Ô trống đầu tiên trên bàn của đợt cho khối cỡ w×h; null nếu bàn đầy.</summary>
        public Vector2Int? FreeSpot(WaveData wave, BlockData b)
        {
            for (int y = 0; y + b.h <= DragSolver.BoardHeight; y++)
                for (int x = 0; x + b.w <= DragSolver.BoardWidth; x++)
                    if (DragSolver.Free(b, x, y, wave.blocks)) return new Vector2Int(x, y);
            return null;
        }

        public static void Swatch(Rect r, Color c, string label = null, bool selected = false)
        {
            EditorGUI.DrawRect(r, c);
            if (selected)
            {
                var o = new Color(1, 1, 1, 0.95f);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), o);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), o);
                EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), o);
                EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), o);
            }
            if (label != null)
            {
                var st = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                st.normal.textColor = TextOn(c);
                GUI.Label(r, label, st);
            }
        }

        public static void Outline(Rect r, Color c, float t = 2)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
