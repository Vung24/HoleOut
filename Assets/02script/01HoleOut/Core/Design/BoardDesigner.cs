using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace HoleOut.Core
{
    public sealed class BoardDesignOptions
    {
        public double Seconds = 60;
        public int Seed = 1;
        public int OracleBudget = 60000;
        public int UnlockStateLimit = 15000;
        public CancellationToken Cancel;
    }

    public sealed class BoardDesignResult
    {
        public List<WaveData> Waves;
        public List<GateData> Gates;
        public int Score;
        /// <summary>Từng bước: "mồi/số lần dời", "|" khi sang đợt mới.</summary>
        public string Detail;
        public int Layouts;
    }

    /// <summary>
    /// Bước 2 của thiết kế tự động: xếp khối lên bàn từng đợt và chọn cổng cho số và thứ tự đã chốt.
    /// Thử ngẫu nhiên nhiều cách xếp, chấm điểm theo lời giải:
    ///   +3 mỗi khối ra được ngay mà lấy là thua (mồi); +6 thêm nếu mồi cùng màu khác số;
    ///   +2 mỗi lần phải dời để lấy khối đúng (tối đa 4); −4 nếu khối đúng ra được ngay;
    ///   −2 mỗi khối ra được ngay mà vẫn còn đường thắng.
    /// Khối số lớn nhất mỗi đợt thành 2×2, lớn thứ hai thành 1×2 dọc.
    /// </summary>
    public static class BoardDesigner
    {
        /// <param name="order">Id khối theo thứ tự lời giải bền.</param>
        public static BoardDesignResult Run(LevelData level, IReadOnlyList<int> order, BoardDesignOptions o)
        {
            var rng = new Random(o.Seed);
            var model = new DrainModel(level);
            var oracle = new WinOracle(model, o.Cancel);

            // Bảng "lấy khối này ở bước k thì hết đường thắng".
            var lose = new List<HashSet<int>>();
            var s = model.Initial();
            var choices = new List<int>();
            foreach (int id in order)
            {
                int idx = model.IndexOfBlockId(id);
                if (idx < 0) throw new ArgumentException($"Không có khối {id}.");
                model.Choices(s, choices);
                var set = new HashSet<int>();
                foreach (int j in choices)
                    if (j != idx && oracle.CanWin(model.Launch(s, j), o.OracleBudget) == WinVerdict.Lose) set.Add(model.Blocks[j].id);
                lose.Add(set);
                model.LaunchInPlace(s, idx);
            }

            var shapes = new Dictionary<int, (int w, int h)>();
            foreach (var w in level.waves)
            {
                var sorted = new List<BlockData>(w.blocks);
                sorted.Sort((a, b) => b.capacity.CompareTo(a.capacity));
                if (sorted.Count > 0) shapes[sorted[0].id] = (2, 2);
                if (sorted.Count > 1) shapes[sorted[1].id] = (1, 2);
            }

            var watch = Stopwatch.StartNew();
            BoardDesignResult best = null;
            int n = 0;
            while (watch.Elapsed.TotalSeconds < o.Seconds)
            {
                o.Cancel.ThrowIfCancellationRequested();
                var layout = RandomLayout(level, shapes, rng);
                if (layout == null) continue;
                n++;
                var r = Evaluate(level, layout.Value.waves, layout.Value.gates, order, lose, o);
                if (r == null) continue;
                if (best == null || r.Score > best.Score) best = r;
            }
            if (best != null) best.Layouts = n;
            return best;
        }

        static (List<WaveData> waves, List<GateData> gates)? RandomLayout(LevelData level, Dictionary<int, (int w, int h)> shapes, Random rng)
        {
            var waves = new List<WaveData>();
            var colorsLen = new Dictionary<string, int>();
            foreach (var w in level.waves)
            {
                var ids = new List<BlockData>(w.blocks);
                // Khối to xếp trước, cùng cỡ thì xáo ngẫu nhiên.
                var keys = new Dictionary<int, double>();
                foreach (var b in ids) keys[b.id] = (shapes.TryGetValue(b.id, out var sh) ? sh.w * sh.h : 1) + rng.NextDouble() * 0.5;
                ids.Sort((a, b) => keys[b.id].CompareTo(keys[a.id]));
                var placed = new WaveData();
                foreach (var src in ids)
                {
                    var b = new BlockData { id = src.id, color = src.color, capacity = src.capacity, w = 1, h = 1 };
                    if (shapes.TryGetValue(b.id, out var sh)) { b.w = sh.w; b.h = sh.h; }
                    var spots = new List<(int, int)>();
                    for (int y = 0; y + b.h <= DragSolver.BoardHeight; y++)
                        for (int x = 0; x + b.w <= DragSolver.BoardWidth; x++)
                            if (DragSolver.Free(b, x, y, placed.blocks)) spots.Add((x, y));
                    if (spots.Count == 0) return null;
                    var (px, py) = spots[rng.Next(spots.Count)];
                    b.gx = px; b.gy = py;
                    placed.blocks.Add(b);
                    colorsLen[b.color] = Math.Max(colorsLen.TryGetValue(b.color, out var l) ? l : 1, Math.Max(b.w, b.h));
                }
                placed.blocks.Sort((a, b) => a.id.CompareTo(b.id));
                waves.Add(placed);
            }

            var gates = new List<GateData>();
            var colors = new List<string>(colorsLen.Keys);
            colors.Sort(string.CompareOrdinal);
            foreach (var c in colors)
            {
                int len = colorsLen[c];
                var cand = new List<GateData>();
                foreach (GateSide side in Enum.GetValues(typeof(GateSide)))
                {
                    int limit = side == GateSide.Top || side == GateSide.Bottom ? DragSolver.BoardWidth : DragSolver.BoardHeight;
                    for (int st = 0; st + len <= limit; st++)
                    {
                        bool overlap = false;
                        foreach (var g in gates) overlap |= g.side == side && st < g.start + g.len && st + len > g.start;
                        if (!overlap) cand.Add(new GateData { color = c, side = side, start = st, len = len });
                    }
                }
                if (cand.Count == 0) return null;
                gates.Add(cand[rng.Next(cand.Count)]);
            }
            return (waves, gates);
        }

        static BoardDesignResult Evaluate(LevelData level, List<WaveData> waves, List<GateData> gates,
            IReadOnlyList<int> order, List<HashSet<int>> lose, BoardDesignOptions o)
        {
            int wi = -1, score = 0;
            var board = new List<BlockData>();
            var detail = new StringBuilder();
            for (int k = 0; k < order.Count; k++)
            {
                if (board.Count == 0)
                {
                    wi++;
                    if (wi >= waves.Count) return null;
                    if (wi > 0) detail.Append(" |");
                    foreach (var b in waves[wi].blocks) board.Add(b.Copy());
                }
                var target = board.Find(b => b.id == order[k]);
                if (target == null) return null;

                int bait = 0, sib = 0, safe = 0;
                foreach (var b in board)
                {
                    if (b == target || !BoardReach.CanExit(b, board, gates)) continue;
                    if (lose[k].Contains(b.id))
                    {
                        bait++;
                        if (b.color == target.color && b.capacity != target.capacity) sib++;
                    }
                    else safe++;
                }
                var moves = BoardReach.Unlock(target, board, gates, o.UnlockStateLimit);
                if (moves == null) return null;
                int need = moves.Count;
                score += 3 * bait + 6 * sib + Math.Min(need, 4) * 2 - (need == 0 ? 4 : 0) - 2 * safe;
                detail.Append($" {bait}{(sib > 0 ? $"({sib} sai số)" : "")}/{need}");
                board.Remove(target);
            }

            // Trả vị trí ban đầu (BoardReach đã dời bản sao, không chạm vào waves).
            return new BoardDesignResult { Waves = waves, Gates = gates, Score = score, Detail = detail.ToString().Trim() };
        }
    }
}
