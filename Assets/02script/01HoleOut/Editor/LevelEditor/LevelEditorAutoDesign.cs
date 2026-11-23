using System.Collections.Generic;
using System.Linq;
using HoleOut.Core;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Nút "Tự chia số" (tab Số & Đợt) và "Tự xếp bàn" (tab Bàn & Cổng), chạy ở luồng nền.</summary>
    sealed class LevelEditorAutoDesign
    {
        readonly LevelEditorContext _ctx;
        readonly LevelEditorJob<CapsDesignResult> _capsJob = new LevelEditorJob<CapsDesignResult>();
        readonly LevelEditorJob<(BoardDesignResult board, List<int> order)> _boardJob = new LevelEditorJob<(BoardDesignResult, List<int>)>();
        float _seconds = 60;
        int _seed = 1, _waves = 2, _minPerWave = 6, _maxPerWave = 11;
        bool _continueFromCurrent;
        string _capsMessage, _boardMessage;

        /// <summary>Gọi sau khi áp bố trí bàn mới, với thứ tự id lời giải bền.</summary>
        public System.Action<List<int>> BoardApplied;

        public LevelEditorAutoDesign(LevelEditorContext ctx) => _ctx = ctx;

        public bool Running => _capsJob.Running || _boardJob.Running;

        public void Cancel() { _capsJob.Cancel(); _boardJob.Cancel(); }

        public bool Poll() => _capsJob.Poll() | _boardJob.Poll();

        void DrawRunning<T>(LevelEditorJob<T> job)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{job.Label}… {job.Elapsed:0}/{_seconds:0}s", EditorStyles.boldLabel);
                if (GUILayout.Button("Hủy", GUILayout.Width(60))) job.Cancel();
            }
        }

        // ───────────── Tự chia số ─────────────

        public void DrawCaps()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Tự chia số", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Máy chọn số trên khối và chia đợt để tỉ lệ thắng ngẫu nhiên thấp, nhiều bẫy sai số, " +
                                           "người chơi tham thua. Vị trí khối được đặt tạm; chạy Tự xếp bàn sau đó.", EditorStyles.wordWrappedMiniLabel);
                if (_capsJob.Running) { DrawRunning(_capsJob); return; }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _waves = EditorGUILayout.IntSlider("Số đợt", _waves, 1, 3);
                    _seconds = EditorGUILayout.FloatField("Thời gian (giây)", _seconds);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _minPerWave = EditorGUILayout.IntField("Khối mỗi đợt từ", _minPerWave);
                    _maxPerWave = EditorGUILayout.IntField("đến", _maxPerWave);
                    _seed = EditorGUILayout.IntField("Seed", _seed);
                }
                _continueFromCurrent = EditorGUILayout.ToggleLeft("Chạy tiếp từ số hiện tại (nếu tổng đã khớp tranh)", _continueFromCurrent);
                if (GUILayout.Button("Tự chia số")) StartCaps();
                if (!string.IsNullOrEmpty(_capsMessage)) EditorGUILayout.HelpBox(_capsMessage, MessageType.Info);
            }
        }

        void StartCaps()
        {
            var level = _ctx.Level;
            var picture = LevelEditorJob<CapsDesignResult>.Snapshot(level.data);
            List<NumberBlock> start = null;
            if (_continueFromCurrent)
            {
                start = new List<NumberBlock>();
                for (int w = 0; w < picture.waves.Count && w < _waves; w++)
                    foreach (var b in picture.waves[w].blocks) start.Add(new NumberBlock { color = b.color, capacity = b.capacity, wave = w });
            }
            var options = new CapsDesignOptions
            {
                Seconds = Mathf.Max(5, _seconds), Seed = _seed, Waves = _waves,
                MinPerWave = Mathf.Max(1, _minPerWave), MaxPerWave = Mathf.Max(_minPerWave, _maxPerWave)
            };
            _capsMessage = null;
            _capsJob.Start("Đang chia số", token => { options.Cancel = token; return CapsDesigner.Run(picture, start, options); },
                result =>
                {
                    if (_ctx.Level != level) return;
                    if (result == null) { _capsMessage = "Không tìm được cách chia số có lời giải bền. Thử thêm thời gian, đổi seed hoặc nới số khối mỗi đợt."; return; }
                    ApplyCaps(result, options.Waves);
                    var r = result.Report;
                    _capsMessage = $"Đã áp dụng ({result.Iterations} lần thử): thắng ngẫu nhiên {100f * r.RandomWinRate:0.00}%, " +
                                   $"bẫy sai số {r.wrongCapacityTraps}, người chơi tham thua {r.greedyLosses}/4.";
                },
                ex => _capsMessage = "Lỗi: " + ex.Message);
        }

        void ApplyCaps(CapsDesignResult result, int waves)
        {
            var data = _ctx.Data;
            _ctx.Record("Tự chia số");
            data.waves.Clear();
            for (int w = 0; w < waves; w++) data.waves.Add(new WaveData());
            for (int i = 0; i < result.Blocks.Count; i++)
            {
                var nb = result.Blocks[i];
                var b = new BlockData { id = i, color = nb.color, capacity = nb.capacity };
                var spot = _ctx.FreeSpot(data.waves[nb.wave], b);
                if (spot.HasValue) { b.gx = spot.Value.x; b.gy = spot.Value.y; }
                data.waves[nb.wave].blocks.Add(b);
            }
            _ctx.Changed();
        }

        // ───────────── Tự xếp bàn ─────────────

        public void DrawBoard()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Tự xếp bàn", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Giữ số và đợt hiện tại; máy chọn hình khối, vị trí và cổng để mỗi bước có mồi " +
                                           "và khối đúng phải dời mới lấy được. Xong sẽ dựng và kiểm chứng lời giải.", EditorStyles.wordWrappedMiniLabel);
                if (_boardJob.Running) { DrawRunning(_boardJob); return; }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _seconds = EditorGUILayout.FloatField("Thời gian (giây)", _seconds);
                    _seed = EditorGUILayout.IntField("Seed", _seed);
                }
                if (GUILayout.Button("Tự xếp bàn")) StartBoard();
                if (!string.IsNullOrEmpty(_boardMessage)) EditorGUILayout.HelpBox(_boardMessage, MessageType.Info);
            }
        }

        void StartBoard()
        {
            var level = _ctx.Level;
            var data = LevelEditorJob<CapsDesignResult>.Snapshot(level.data);
            var options = new BoardDesignOptions { Seconds = Mathf.Max(5, _seconds), Seed = _seed };
            _boardMessage = null;
            _boardJob.Start("Đang xếp bàn", token =>
                {
                    options.Cancel = token;
                    var report = DifficultyAnalyzer.Analyze(data, null, new DifficultyOptions { Validate = false, AnalyzeBoard = false, Cancel = token });
                    if (!report.solvable) return (null, null);
                    var flat = data.waves.SelectMany(w => w.blocks).ToList();
                    var order = report.referenceOrder.Select(i => flat[i].id).ToList();
                    return (BoardDesigner.Run(data, order, options), order);
                },
                result =>
                {
                    if (_ctx.Level != level) return;
                    if (result.order == null) { _boardMessage = "Số và đợt hiện tại chưa có lời giải bền. Chỉnh số hoặc chạy Tự chia số trước."; return; }
                    if (result.board == null) { _boardMessage = "Không tìm được bố trí bàn đưa được mọi khối ra. Thử thêm thời gian hoặc đổi seed."; return; }
                    _ctx.Record("Tự xếp bàn");
                    _ctx.Data.waves = result.board.Waves;
                    _ctx.Data.gates = result.board.Gates;
                    _ctx.Changed();
                    _boardMessage = $"Đã áp dụng (điểm {result.board.Score}, {result.board.Layouts} cách xếp đã thử). Mồi/số lần dời từng bước: {result.board.Detail}";
                    BoardApplied?.Invoke(result.order);
                },
                ex => _boardMessage = "Lỗi: " + ex.Message);
        }
    }
}
