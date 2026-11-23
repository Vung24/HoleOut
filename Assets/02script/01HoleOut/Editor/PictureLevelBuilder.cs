using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoleOut.Core;
using HoleOut.Runtime;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>
    /// Dựng màn từ file tranh trong <see cref="PictureFolder"/>: chia số khối, xếp bàn, dựng và kiểm chứng lời giải
    /// ở luồng nền, rồi ghi LevelSO (hiện trọn tranh, ô chuẩn).
    /// File tranh: các dòng "khóa: giá trị", một dòng "---", sau đó mỗi dòng là một hàng tranh (từ trên xuống) theo mã màu.
    /// Khóa: id, name, tier, dock, waves, seconds (bỏ trống thì lấy theo cấp ở LevelDesign.Presets), seed (1), minPerWave (6), maxPerWave (11).
    /// </summary>
    [InitializeOnLoad]
    public static class PictureLevelBuilder
    {
        // Hàng đợi lưu ở Library/ (không vào build/VCS): sống qua lần tải lại code và cả khi tắt/mở lại Unity, tự chạy tiếp.
        const string QueueFile = "Library/HoleOutBuildQueue.txt";

        static PictureLevelBuilder()
        {
            string saved = File.Exists(QueueFile) ? File.ReadAllText(QueueFile).Trim() : "";
            if (saved.Length > 0) EditorApplication.delayCall += () => Build(saved.Split(','), resume: true);
        }

        public const string PictureFolder = "Assets/07data/HoleOutPictures";
        public const string LevelFolder = "Assets/07data/HoleOutLevels";

        sealed class PictureSpec
        {
            public string File, Id, Name, Tier;
            public int Dock, Waves, Seed = 1, MinPerWave = 6, MaxPerWave = 11;
            public double Seconds;
            public string[] Grid;
        }

        sealed class BuildResult
        {
            public PictureSpec Spec;
            public LevelData Data;
            public LevelSolution Solution;
            public DifficultyReport Report;
            public string Message;
        }

        static readonly Queue<PictureSpec> Pending = new Queue<PictureSpec>();
        static Task<BuildResult> _running;
        static string _current;

        /// <summary>Ghi các tranh chưa xong (kể cả tranh đang dựng) để chạy tiếp nếu Unity tải lại code.</summary>
        static void SaveQueue()
        {
            var ids = new List<string>();
            if (_current != null) ids.Add(_current);
            ids.AddRange(Pending.Select(p => p.Id));
            if (ids.Count == 0) { if (File.Exists(QueueFile)) File.Delete(QueueFile); }
            else File.WriteAllText(QueueFile, string.Join(",", ids));
        }
        static CancellationTokenSource _cancel;

        [MenuItem("Tools/HoleOut/Dựng màn từ tranh (tất cả)")]
        public static void BuildAll() => Build(null);

        [MenuItem("Tools/HoleOut/Dựng màn từ tranh (dừng)")]
        public static void Stop()
        {
            Pending.Clear();
            if (File.Exists(QueueFile)) File.Delete(QueueFile);
            _cancel?.Cancel();
        }

        /// <summary>Đưa các tranh vào hàng đợi; <paramref name="ids"/> null là tất cả.</summary>
        public static void Build(IEnumerable<string> ids) => Build(ids, false);

        static void Build(IEnumerable<string> ids, bool resume)
        {
            var specs = Directory.GetFiles(PictureFolder, "*.txt").OrderBy(p => p).Select(Parse).Where(s => s != null).ToList();
            var byId = specs.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            var order = ids != null ? ids.ToList() : specs.Select(s => s.Id).ToList();
            var queued = new HashSet<string>(Pending.Select(p => p.Id));
            if (_current != null) queued.Add(_current);
            // Giữ đúng thứ tự người gọi đưa vào (tranh ưu tiên đứng trước).
            foreach (var id in order)
                if (byId.TryGetValue(id, out var spec) && queued.Add(id)) Pending.Enqueue(spec);
            SaveQueue();
            Debug.Log($"[HoleOut] Hàng đợi dựng màn{(resume ? " (chạy tiếp sau khi Unity tải lại code)" : "")}: {Pending.Count} tranh.");
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        public static bool Busy => _running != null || Pending.Count > 0;

        static void Poll()
        {
            if (_running != null)
            {
                if (!_running.IsCompleted) return;
                var task = _running;
                _running = null;
                if (task.IsFaulted) Debug.LogError("[HoleOut] Dựng màn lỗi: " + task.Exception?.GetBaseException().Message);
                else if (!task.IsCanceled) Save(task.Result);
                _current = null;
                SaveQueue();
            }
            if (Pending.Count == 0) { EditorApplication.update -= Poll; return; }
            var spec = Pending.Dequeue();
            _current = spec.Id;
            Debug.Log($"[HoleOut] Đang dựng {spec.Id} ({spec.Grid[0].Length}×{spec.Grid.Length})…");
            _cancel = new CancellationTokenSource();
            var token = _cancel.Token;
            _running = Task.Run(() => Run(spec, token), token);
        }

        static BuildResult Run(PictureSpec spec, CancellationToken token)
        {
            var r = LevelDesign.Run(spec.Id, spec.Grid, spec.Tier, DifficultyTier.Defaults(), spec.Seed, token, spec.Dock, spec.Waves, spec.Seconds);
            return new BuildResult { Spec = spec, Data = r?.Data, Solution = r?.Solution, Report = r?.Report, Message = r?.Message ?? "không dựng được" };
        }

        static void Save(BuildResult r)
        {
            if (r.Data == null || r.Solution == null)
            {
                Debug.LogWarning($"[HoleOut] {r.Spec.Id}: {r.Message}");
                return;
            }
            if (!AssetDatabase.IsValidFolder(LevelFolder)) AssetDatabase.CreateFolder("Assets/07data", "HoleOutLevels");
            string path = $"{LevelFolder}/{r.Spec.Id}.asset";
            var so = AssetDatabase.LoadAssetAtPath<LevelSO>(path);
            bool created = so == null;
            if (created) so = ScriptableObject.CreateInstance<LevelSO>();
            so.displayName = r.Spec.Name;
            so.source = Path.GetFileName(r.Spec.File);
            so.data = r.Data;
            so.solution = r.Solution;
            so.solution.dataVersion = LevelImporter.DataVersion(r.Data);
            so.lastReplay = r.Message;
            if (r.Report != null) { so.lastReport = r.Report; so.lastReportVersion = so.solution.dataVersion; }
            if (!string.IsNullOrEmpty(r.Spec.Tier)) so.targetTier = r.Spec.Tier;
            if (created) AssetDatabase.CreateAsset(so, path);
            else EditorUtility.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HoleOut] Đã ghi {path}: {r.Message}");
        }

        static PictureSpec Parse(string path)
        {
            var lines = File.ReadAllLines(path).Select(l => l.TrimEnd()).ToList();
            int split = lines.IndexOf("---");
            if (split < 0) { Debug.LogWarning($"[HoleOut] {path}: thiếu dòng ---"); return null; }
            var spec = new PictureSpec { File = path, Id = Path.GetFileNameWithoutExtension(path) };
            foreach (var line in lines.Take(split))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string key = line.Substring(0, colon).Trim(), value = line.Substring(colon + 1).Trim();
                switch (key)
                {
                    case "id": spec.Id = value; break;
                    case "name": spec.Name = value; break;
                    case "tier": spec.Tier = value; break;
                    case "dock": int.TryParse(value, out spec.Dock); break;
                    case "waves": int.TryParse(value, out spec.Waves); break;
                    case "seed": int.TryParse(value, out spec.Seed); break;
                    case "minPerWave": int.TryParse(value, out spec.MinPerWave); break;
                    case "maxPerWave": int.TryParse(value, out spec.MaxPerWave); break;
                    case "seconds": double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out spec.Seconds); break;
                }
            }
            var preset = LevelDesign.Preset(spec.Tier);
            if (spec.Dock <= 0) spec.Dock = preset.Dock;
            if (spec.Waves <= 0) spec.Waves = preset.Waves;
            if (spec.Seconds <= 0) spec.Seconds = preset.Seconds;
            spec.Grid = lines.Skip(split + 1).Where(l => l.Length > 0).ToArray();
            if (spec.Grid.Length == 0 || spec.Grid.Any(l => l.Length != spec.Grid[0].Length))
            {
                Debug.LogWarning($"[HoleOut] {path}: các hàng tranh phải cùng độ dài.");
                return null;
            }
            return spec;
        }
    }
}
