using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace HoleOut.Core
{
    [Serializable]
    public struct NumberBlock
    {
        public string color;
        public int capacity;
        public int wave;
    }

    public sealed class CapsDesignOptions
    {
        public double Seconds = 60;
        public int Seed = 1;
        public int Waves = 2;
        public int MinPerWave = 6;
        public int MaxPerWave = 11;
        public int MinCapacity = 3;
        /// <summary>Hai khối cùng màu, cùng đợt phải lệch ít nhất chừng này (tránh bẫy giả).</summary>
        public int MinGap = 3;
        public CancellationToken Cancel;
    }

    public sealed class CapsDesignResult
    {
        public List<NumberBlock> Blocks;
        /// <summary>Thứ tự lời giải bền, là chỉ số trong <see cref="Blocks"/>.</summary>
        public List<int> Order;
        public DifficultyReport Report;
        public double Score;
        public int Iterations;
    }

    /// <summary>
    /// Bước 1 của thiết kế tự động: chọn số trên khối và chia đợt cho một bức tranh cố định.
    /// Leo đồi trên các đột biến nhỏ, giữ bản có điểm thấp nhất:
    /// J = log10(tỉ lệ thắng ngẫu nhiên) − 0,35 × bẫy sai số − 0,8 × số kiểu người chơi tham bị thua.
    /// Bắt buộc có lời giải bền và không có hai khối cùng màu, cùng đợt lệch dưới <see cref="CapsDesignOptions.MinGap"/>.
    /// </summary>
    public static class CapsDesigner
    {
        public static CapsDesignResult Run(LevelData picture, List<NumberBlock> start, CapsDesignOptions o)
        {
            var rng = new Random(o.Seed);
            var counts = new Dictionary<string, int>();
            var firstRow = new Dictionary<string, int>();
            for (int r = picture.Rows - 1; r >= 0; r--)
                foreach (char ch in picture.grid[r])
                {
                    string k = ch.ToString();
                    counts[k] = counts.TryGetValue(k, out var n) ? n + 1 : 1;
                    if (!firstRow.ContainsKey(k)) firstRow[k] = picture.Rows - 1 - r;
                }

            var cur = start != null && start.Count > 0 && Sums(start, counts) ? new List<NumberBlock>(start) : Initial(counts, firstRow, o, rng);
            for (int t = 0; t < 5000 && !Valid(cur, o, counts); t++)
            {
                var m = Mutate(cur, o, rng);
                if (m != null) cur = m;
            }

            var watch = Stopwatch.StartNew();
            CapsDesignResult best = Valid(cur, o, counts) ? Score(picture, cur, o) : null;
            int it = 0;
            while (watch.Elapsed.TotalSeconds < o.Seconds)
            {
                o.Cancel.ThrowIfCancellationRequested();
                it++;
                var n = Mutate(cur, o, rng);
                if (n == null || !Valid(n, o, counts)) continue;
                var s = Score(picture, n, o);
                if (s == null) continue;
                if (best == null || s.Score < best.Score) { cur = n; best = s; }
            }
            if (best != null) best.Iterations = it;
            return best;
        }

        static bool Sums(List<NumberBlock> bs, Dictionary<string, int> counts)
        {
            var sum = new Dictionary<string, int>();
            foreach (var b in bs) sum[b.color] = (sum.TryGetValue(b.color, out var n) ? n : 0) + b.capacity;
            if (sum.Count != counts.Count) return false;
            foreach (var kv in counts) if (!sum.TryGetValue(kv.Key, out var v) || v != kv.Value) return false;
            return true;
        }

        /// <summary>Dữ liệu màn chỉ dùng cho mô hình số: vị trí khối không quan trọng.</summary>
        public static LevelData NumbersLevel(LevelData picture, List<NumberBlock> bs, int waves)
        {
            var level = new LevelData
            {
                levelId = picture.levelId, grid = picture.grid, displayMode = picture.displayMode,
                visibleRows = picture.visibleRows, pictureScale = picture.pictureScale, dockSlots = picture.dockSlots
            };
            for (int w = 0; w < waves; w++) level.waves.Add(new WaveData());
            for (int i = 0; i < bs.Count; i++)
                level.waves[bs[i].wave].blocks.Add(new BlockData { id = i, color = bs[i].color, capacity = bs[i].capacity });
            return level;
        }

        static CapsDesignResult Score(LevelData picture, List<NumberBlock> bs, CapsDesignOptions o)
        {
            var level = NumbersLevel(picture, bs, o.Waves);
            var report = DifficultyAnalyzer.Analyze(level, null, new DifficultyOptions
            {
                Validate = false, AnalyzeBoard = false, SearchNodeLimit = 150000, OracleBudget = 20000, Cancel = o.Cancel
            });
            if (!report.solvable || !report.robust) return null;
            // referenceOrder là chỉ số trong mô hình (theo đợt); đổi về chỉ số trong bs qua id.
            var flat = new List<BlockData>();
            foreach (var w in level.waves) flat.AddRange(w.blocks);
            var order = new List<int>();
            foreach (int idx in report.referenceOrder) order.Add(flat[idx].id);
            double win = Math.Max(report.randomWins, 0.5) / Math.Max(1, report.randomSamples);
            return new CapsDesignResult
            {
                Blocks = new List<NumberBlock>(bs), Order = order, Report = report,
                Score = Math.Log10(win) - 0.35 * report.wrongCapacityTraps - 0.8 * report.greedyLosses
            };
        }

        static bool Valid(List<NumberBlock> bs, CapsDesignOptions o, Dictionary<string, int> counts)
        {
            var perWave = new int[o.Waves];
            foreach (var b in bs)
            {
                // Màu ít ô hơn mức tối thiểu vẫn được một khối bằng đúng số ô của nó.
                int min = Math.Min(o.MinCapacity, counts.TryGetValue(b.color, out var total) ? total : o.MinCapacity);
                if (b.wave < 0 || b.wave >= o.Waves || b.capacity < min) return false;
                perWave[b.wave]++;
            }
            foreach (int n in perWave) if (n < o.MinPerWave || n > o.MaxPerWave) return false;
            for (int i = 0; i < bs.Count; i++)
                for (int j = i + 1; j < bs.Count; j++)
                    if (bs[i].color == bs[j].color && bs[i].wave == bs[j].wave && Math.Abs(bs[i].capacity - bs[j].capacity) < o.MinGap)
                        return false;
            return true;
        }

        static List<NumberBlock> Initial(Dictionary<string, int> counts, Dictionary<string, int> firstRow, CapsDesignOptions o, Random rng)
        {
            var bs = new List<NumberBlock>();
            foreach (var kv in counts)
            {
                int k = kv.Value >= 60 ? 3 : kv.Value >= 20 ? 2 : 1, left = kv.Value;
                for (int i = k; i >= 1; i--)
                {
                    int v = i == 1 ? left : (int)Math.Round(left / (double)i * (0.6 + 0.8 * rng.NextDouble()));
                    bs.Add(new NumberBlock { color = kv.Key, capacity = v });
                    left -= v;
                }
            }
            bs.Sort((a, b) => firstRow[a.color].CompareTo(firstRow[b.color]));
            for (int i = 0; i < bs.Count; i++)
            {
                var b = bs[i];
                b.wave = Math.Min(o.Waves - 1, i * o.Waves / bs.Count);
                bs[i] = b;
            }
            return bs;
        }

        static List<NumberBlock> Mutate(List<NumberBlock> src, CapsDesignOptions o, Random rng)
        {
            var n = new List<NumberBlock>(src);
            double t = rng.NextDouble();
            int i = rng.Next(n.Count);
            var same = new List<int>();
            for (int j = 0; j < n.Count; j++) if (j != i && n[j].color == n[i].color) same.Add(j);

            if (t < 0.3)
            {
                if (o.Waves < 2) return null;
                var b = n[i]; b.wave = (b.wave + 1 + rng.Next(o.Waves - 1)) % o.Waves; n[i] = b;
            }
            else if (t < 0.65)
            {
                if (same.Count == 0) return null;
                int j = same[rng.Next(same.Count)], d = 1 + rng.Next(8);
                var a = n[i]; var b = n[j];
                a.capacity -= d; b.capacity += d;
                n[i] = a; n[j] = b;
            }
            else if (t < 0.85)
            {
                var a = n[i];
                if (a.capacity < 8) return null;
                int p = 3 + rng.Next(a.capacity - 5);
                a.capacity -= p;
                n[i] = a;
                n.Add(new NumberBlock { color = a.color, capacity = p, wave = rng.Next(o.Waves) });
            }
            else
            {
                if (same.Count == 0) return null;
                int j = same[rng.Next(same.Count)];
                var b = n[j]; b.capacity += n[i].capacity; n[j] = b;
                n.RemoveAt(i);
            }
            return n;
        }
    }
}
