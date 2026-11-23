using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Cửa sổ thiết kế màn HoleOut: vẽ tranh, chia số và đợt, xếp bàn và cổng, đo độ khó.
    /// Sửa trực tiếp trên asset LevelSO, mọi thao tác Undo được.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        const string PrefSelected = "HoleOut.LevelEditor.Selected";
        const string PrefTab = "HoleOut.LevelEditor.Tab";
        static readonly string[] TabNames = { "Bắt đầu nhanh", "Tranh", "Số & Đợt", "Bàn & Cổng", "Phân tích", "Lời giải", "Chiến dịch" };
        public const int QuickTab = 0, PictureTab = 1, CampaignTab = 6;
        const int DefaultSize = 20;

        readonly LevelEditorContext _ctx = new LevelEditorContext();
        LevelEditorPictureTab _picture;
        LevelEditorNumbersTab _numbers;
        LevelEditorBoardTab _board;
        LevelEditorAnalysisTab _analysis;
        LevelEditorSolutionTab _solution;
        LevelEditorCampaignTab _campaign;
        LevelEditorAutoDesign _auto;
        LevelEditorQuickTab _quick;

        List<LevelSO> _levels = new List<LevelSO>();
        DifficultyProfileSO _profile;
        List<DifficultyTier> _defaultTiers;
        Vector2 _listScroll, _bodyScroll;
        string _search = "";
        int _tab;
        bool _showErrors;
        int _errorsRevision = -1;
        List<string> _errors = new List<string>();

        [MenuItem("Tools/HoleOut/Level Editor")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor").Show();

        public static void Open(LevelSO level)
        {
            var w = GetWindow<LevelEditorWindow>("Level Editor");
            w.Select(level);
            w.Show();
        }

        internal IList<DifficultyTier> Tiers => _profile != null ? (IList<DifficultyTier>)_profile.tiers : _defaultTiers;
        internal DifficultyProfileSO Profile => _profile;
        internal IReadOnlyList<LevelSO> Levels => _levels;
        internal LevelSO Selected => _ctx.Level;
        internal void SelectLevel(LevelSO level) => Select(level);

        void OnEnable()
        {
            minSize = new Vector2(820, 520);
            _ctx.Window = this;
            _picture = new LevelEditorPictureTab(_ctx);
            _auto = new LevelEditorAutoDesign(_ctx);
            _numbers = new LevelEditorNumbersTab(_ctx, _auto);
            _board = new LevelEditorBoardTab(_ctx, _auto);
            _analysis = new LevelEditorAnalysisTab(_ctx, this);
            _solution = new LevelEditorSolutionTab(_ctx);
            _campaign = new LevelEditorCampaignTab(this);
            _quick = new LevelEditorQuickTab(_ctx, this);
            _auto.BoardApplied = order => _solution.BuildAndVerify(order);
            _defaultTiers = DifficultyTier.Defaults();
            _tab = EditorPrefs.GetInt(PrefTab, 0);
            Undo.undoRedoPerformed += OnUndoRedo;
            RefreshList();
            var guid = EditorPrefs.GetString(PrefSelected, "");
            var saved = string.IsNullOrEmpty(guid) ? null : AssetDatabase.LoadAssetAtPath<LevelSO>(AssetDatabase.GUIDToAssetPath(guid));
            Select(saved != null ? saved : _levels.FirstOrDefault());
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            _analysis?.Cancel();
            _solution?.Cancel();
            _auto?.Cancel();
            _quick?.Cancel();
            _campaign?.Dispose();
        }

        /// <summary>Nhận kết quả các việc chạy nền và vẽ lại khi cần (~10 lần/giây).</summary>
        void Update()
        {
            bool repaint = _auto.Poll() | _solution.Poll() | _campaign.Poll() | _quick.Poll();
            _analysis.Tick();
            if (repaint || _auto.Running || _quick.Running) Repaint();
        }

        void OnProjectChange() => RefreshList();

        void OnUndoRedo()
        {
            _ctx.Revision++;
            Repaint();
        }

        /// <summary>
        /// Danh sách màn và bộ cấp độ lấy bằng AssetDatabase: cửa sổ editor phải thấy mọi asset trong dự án,
        /// không có scene/prefab nào để gắn sẵn. Chỉ chạy khi mở cửa sổ hoặc dự án đổi.
        /// </summary>
        void RefreshList()
        {
            _levels = AssetDatabase.FindAssets("t:LevelSO")
                .Select(g => AssetDatabase.LoadAssetAtPath<LevelSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(l => l != null).OrderBy(l => AssetDatabase.GetAssetPath(l)).ToList();
            var profileGuid = AssetDatabase.FindAssets("t:DifficultyProfileSO").FirstOrDefault();
            _profile = profileGuid == null ? null : AssetDatabase.LoadAssetAtPath<DifficultyProfileSO>(AssetDatabase.GUIDToAssetPath(profileGuid));
            Repaint();
        }

        void Select(LevelSO level)
        {
            if (_ctx.Level == level) return;
            _analysis?.Cancel();
            _solution?.Cancel();
            _auto?.Cancel();
            _quick?.Cancel();
            _ctx.Level = level;
            _ctx.Revision++;
            if (level != null)
            {
                EditorPrefs.SetString(PrefSelected, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(level)));
                var colors = _ctx.PictureColors();
                if (colors.Count > 0 && colors.All(c => c.Key != _ctx.Brush)) _ctx.Brush = colors[0].Key;
            }
            Repaint();
        }

        internal string MeasuredTierName(LevelSO level)
        {
            if (level == null || level.lastReport == null || level.lastReportVersion == 0) return "";
            int i = DifficultyTier.Measure(Tiers, level.lastReport);
            return i < 0 ? "chưa đạt" : Tiers[i].name;
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                using (new EditorGUILayout.VerticalScope())
                {
                    if (_ctx.Level == null)
                    {
                        EditorGUILayout.HelpBox("Chọn một màn bên trái hoặc tạo màn mới.", MessageType.Info);
                        return;
                    }
                    DrawHeader();
                    int tab = GUILayout.Toolbar(_tab, TabNames, GUILayout.Height(24));
                    if (tab != _tab) { _tab = tab; EditorPrefs.SetInt(PrefTab, tab); GUI.FocusControl(null); }
                    _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);
                    switch (_tab)
                    {
                        case QuickTab: _quick.Draw(); break;
                        case PictureTab: _picture.Draw(); break;
                        case 2: _numbers.Draw(); break;
                        case 3: _board.Draw(); break;
                        case 4: _analysis.Draw(); break;
                        case 5: _solution.Draw(); break;
                        default: _campaign.Draw(); break;
                    }
                    EditorGUILayout.EndScrollView();
                    DrawStatus();
                }
            }
        }

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Màn mới")) CreateLevel();
                    if (GUILayout.Button("↻", GUILayout.Width(26))) RefreshList();
                }
                _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                _listScroll = EditorGUILayout.BeginScrollView(_listScroll, "box");
                foreach (var l in _levels)
                {
                    string label = string.IsNullOrEmpty(l.displayName) ? l.name : $"{l.displayName}  ({l.name})";
                    if (!string.IsNullOrEmpty(_search) && label.IndexOf(_search, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string tier = MeasuredTierName(l);
                    bool stale = l.lastReportVersion != 0 && l.lastReportVersion != LevelImporter.DataVersion(l.data);
                    var style = new GUIStyle(l == _ctx.Level ? EditorStyles.whiteLabel : EditorStyles.label) { richText = true };
                    var r = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Height(20));
                    if (l == _ctx.Level) EditorGUI.DrawRect(r, new Color(0.24f, 0.48f, 0.9f, 0.6f));
                    GUI.Label(new Rect(r.x + 4, r.y, r.width - 70, r.height), label, style);
                    GUI.Label(new Rect(r.xMax - 68, r.y, 66, r.height), tier + (stale ? "*" : ""), EditorStyles.miniLabel);
                    if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        Select(l);
                        Event.current.Use();
                    }
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField("* kết quả đo đã cũ", EditorStyles.miniLabel);
            }
        }

        void DrawHeader()
        {
            var so = _ctx.Level;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.TextField("Tên hiển thị", so.displayName);
                if (EditorGUI.EndChangeCheck()) { _ctx.Record("Đổi tên màn"); so.displayName = name; _ctx.Changed(); }
                if (GUILayout.Button("Chọn asset", GUILayout.Width(80))) { Selection.activeObject = so; EditorGUIUtility.PingObject(so); }
                if (GUILayout.Button("Nhân bản", GUILayout.Width(72))) Duplicate();
                using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(so)))
                    if (GUILayout.Button("Lưu", GUILayout.Width(52))) AssetDatabase.SaveAssetIfDirty(so);
            }
        }

        void DrawStatus()
        {
            var data = _ctx.Data;
            if (_errorsRevision != _ctx.Revision) { _errors = LevelValidator.Validate(data); _errorsRevision = _ctx.Revision; }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                int cells = data.Rows * data.Columns, cap = _ctx.AllBlocks().Sum(b => b.capacity);
                GUILayout.Label($"Tranh {data.Columns}×{data.Rows} = {cells} ô | sức chứa {cap}", cap == cells ? EditorStyles.label : Bold(Color.red));
                GUILayout.Label($"| {data.waves.Sum(w => w.blocks.Count)} khối, {data.waves.Count} đợt, khay {data.dockSlots}");
                GUILayout.FlexibleSpace();
                bool solutionOk = _ctx.Level.solution != null && _ctx.Level.solution.HasSteps && _ctx.Level.solution.dataVersion == _ctx.DataVersion;
                GUILayout.Label(solutionOk ? "Lời giải: khớp dữ liệu" : "Lời giải: cũ hoặc chưa có", solutionOk ? EditorStyles.label : Bold(new Color(0.9f, 0.55f, 0.1f)));
                string tier = MeasuredTierName(_ctx.Level);
                if (tier != "") GUILayout.Label($"| Đo được: {tier}{(_ctx.Level.lastReportVersion != _ctx.DataVersion ? " (cũ)" : "")}");
                if (_errors.Count > 0)
                {
                    if (GUILayout.Button($"{_errors.Count} lỗi dữ liệu", Bold(Color.red))) _showErrors = !_showErrors;
                }
                else GUILayout.Label("| Dữ liệu hợp lệ", Bold(new Color(0.2f, 0.65f, 0.25f)));
            }
            if (_showErrors && _errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", _errors), MessageType.Error);
        }

        static GUIStyle Bold(Color c)
        {
            var s = new GUIStyle(EditorStyles.boldLabel);
            s.normal.textColor = c;
            s.hover.textColor = c;
            return s;
        }

        internal void ShowTab(int tab)
        {
            _tab = tab;
            EditorPrefs.SetInt(PrefTab, tab);
            GUI.FocusControl(null);
            Repaint();
        }

        internal void PlayTest() => _solution.PlayTest();

        /// <summary>Màn mới: tranh vuông 20×20 một màu, lưu thẳng vào thư mục màn với tên tự đánh số, mở tab Bắt đầu nhanh.</summary>
        void CreateLevel()
        {
            string folder = PictureLevelBuilder.LevelFolder;
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/07data", System.IO.Path.GetFileName(folder));
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/holeout-new.asset");
            var so = CreateInstance<LevelSO>();
            so.displayName = "Màn mới";
            so.source = "editor";
            so.targetTier = "Thường";
            const int size = DefaultSize;
            var row = new string('L', size);
            so.data = new LevelData
            {
                levelId = so.displayName, grid = Enumerable.Repeat(row, size).ToArray(),
                displayMode = PictureDisplayMode.FullPicture, visibleRows = size, dockSlots = 4
            };
            so.data.waves.Add(new WaveData { blocks = { new BlockData { id = 0, gx = 0, gy = 0, color = "L", capacity = size * size } } });
            so.data.gates.Add(new GateData { color = "L", side = GateSide.Top, start = 0, len = 1 });
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();
            RefreshList();
            Select(so);
            ShowTab(QuickTab);
        }

        void Duplicate()
        {
            var src = AssetDatabase.GetAssetPath(_ctx.Level);
            var dst = AssetDatabase.GenerateUniqueAssetPath(src);
            if (!AssetDatabase.CopyAsset(src, dst)) return;
            RefreshList();
            Select(AssetDatabase.LoadAssetAtPath<LevelSO>(dst));
        }
    }
}
