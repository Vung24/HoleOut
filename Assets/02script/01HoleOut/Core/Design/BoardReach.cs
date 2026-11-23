using System.Collections.Generic;

namespace HoleOut.Core
{
    public struct BoardMove
    {
        public int Block;
        public int X, Y;
    }

    /// <summary>
    /// Đường đi rời rạc trên bàn 6×4: khối dịch từng ô theo 4 hướng (dùng <see cref="DragSolver.Free"/>),
    /// thoát khi đứng sát cổng cùng màu đủ rộng (<see cref="DragSolver.FindGate"/>).
    /// Danh sách khối được sửa vị trí tại chỗ trong lúc tìm và trả lại như cũ.
    /// </summary>
    public static class BoardReach
    {
        static readonly int[] Dx = { 0, 1, 0, -1 };
        static readonly int[] Dy = { -1, 0, 1, 0 };

        /// <summary>Khối ra được cổng mà không dời khối khác.</summary>
        public static bool CanExit(BlockData b, List<BlockData> blocks, IReadOnlyList<GateData> gates)
        {
            int ox = b.gx, oy = b.gy;
            var seen = new HashSet<int> { ox + oy * DragSolver.BoardWidth };
            var queue = new Queue<int>();
            queue.Enqueue(ox + oy * DragSolver.BoardWidth);
            bool found = false;
            while (queue.Count > 0 && !found)
            {
                int p = queue.Dequeue();
                int x = p % DragSolver.BoardWidth, y = p / DragSolver.BoardWidth;
                if (AtGate(b, x, y, gates)) { found = true; break; }
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (!DragSolver.Free(b, nx, ny, blocks)) continue;
                    if (seen.Add(nx + ny * DragSolver.BoardWidth)) queue.Enqueue(nx + ny * DragSolver.BoardWidth);
                }
            }
            return found;
        }

        static bool AtGate(BlockData b, int x, int y, IReadOnlyList<GateData> gates) => GateAt(b, x, y, gates).HasValue;

        static GateSide? GateAt(BlockData b, int x, int y, IReadOnlyList<GateData> gates)
        {
            for (int s = 0; s < 4; s++)
                if (DragSolver.FindGate(b, x, y, gates, (GateSide)s) != null) return (GateSide)s;
            return null;
        }

        /// <summary>
        /// Đường ngắn nhất (từng ô, không gồm ô xuất phát) đưa khối tới sát cổng cùng màu, và cạnh cổng đó.
        /// null nếu không ra được mà không dời khối khác.
        /// </summary>
        public static (List<GridPoint> path, GateSide door)? ExitPath(BlockData b, List<BlockData> blocks, IReadOnlyList<GateData> gates)
        {
            int W = DragSolver.BoardWidth;
            int start = b.gx + b.gy * W;
            var prev = new Dictionary<int, int> { [start] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                int x = p % W, y = p / W;
                var door = GateAt(b, x, y, gates);
                if (door.HasValue)
                {
                    var path = new List<GridPoint>();
                    for (int k = p; k != start; k = prev[k]) path.Add(new GridPoint(k % W, k / W));
                    path.Reverse();
                    return (path, door.Value);
                }
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d], nk = nx + ny * W;
                    if (!DragSolver.Free(b, nx, ny, blocks) || prev.ContainsKey(nk)) continue;
                    prev[nk] = p;
                    queue.Enqueue(nk);
                }
            }
            return null;
        }

        /// <summary>
        /// Số lần dời ít nhất (mỗi lần một khối khác dịch một ô) để <paramref name="target"/> ra được cổng.
        /// Trả về danh sách bước dời (có thể rỗng), hoặc null nếu không tìm thấy trong giới hạn trạng thái.
        /// Khi thành công, vị trí các khối trong <paramref name="blocks"/> được đặt theo các bước dời đó.
        /// </summary>
        public static List<BoardMove> Unlock(BlockData target, List<BlockData> blocks, IReadOnlyList<GateData> gates, int stateLimit)
        {
            if (CanExit(target, blocks, gates)) return new List<BoardMove>();

            int n = blocks.Count;
            var start = Capture(blocks);
            var parent = new Dictionary<(ulong, ulong), ((ulong, ulong) prev, BoardMove move)>();
            var queue = new Queue<(ulong, ulong)>();
            var startKey = Pack(start);
            parent[startKey] = (startKey, default);
            queue.Enqueue(startKey);
            var pos = new int[n];

            while (queue.Count > 0 && parent.Count < stateLimit)
            {
                var key = queue.Dequeue();
                Unpack(key, pos);
                for (int i = 0; i < n; i++)
                {
                    var b = blocks[i];
                    if (b == target) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        Apply(blocks, pos);
                        int nx = b.gx + Dx[d], ny = b.gy + Dy[d];
                        if (!DragSolver.Free(b, nx, ny, blocks)) continue;
                        int saved = pos[i];
                        pos[i] = nx + ny * DragSolver.BoardWidth;
                        var nk = Pack(pos);
                        if (!parent.ContainsKey(nk))
                        {
                            parent[nk] = (key, new BoardMove { Block = b.id, X = nx, Y = ny });
                            Apply(blocks, pos);
                            if (CanExit(target, blocks, gates)) return Trace(parent, nk, startKey);
                            queue.Enqueue(nk);
                        }
                        pos[i] = saved;
                    }
                }
            }
            Apply(blocks, start);
            return null;
        }

        static List<BoardMove> Trace(Dictionary<(ulong, ulong), ((ulong, ulong) prev, BoardMove move)> parent, (ulong, ulong) key, (ulong, ulong) start)
        {
            var moves = new List<BoardMove>();
            while (!key.Equals(start))
            {
                var e = parent[key];
                moves.Add(e.move);
                key = e.prev;
            }
            moves.Reverse();
            return moves;
        }

        static int[] Capture(List<BlockData> blocks)
        {
            var p = new int[blocks.Count];
            for (int i = 0; i < p.Length; i++) p[i] = blocks[i].gx + blocks[i].gy * DragSolver.BoardWidth;
            return p;
        }

        static void Apply(List<BlockData> blocks, int[] pos)
        {
            for (int i = 0; i < pos.Length; i++)
            {
                blocks[i].gx = pos[i] % DragSolver.BoardWidth;
                blocks[i].gy = pos[i] / DragSolver.BoardWidth;
            }
        }

        // 5 bit mỗi khối (24 ô), 12 khối mỗi ulong: đủ cho 24 khối.
        static (ulong, ulong) Pack(int[] pos)
        {
            ulong a = 0, b = 0;
            for (int i = 0; i < pos.Length; i++)
            {
                ulong v = (ulong)(pos[i] + 1);
                if (i < 12) a |= v << (5 * i);
                else b |= v << (5 * (i - 12));
            }
            return (a, b);
        }

        static void Unpack((ulong, ulong) key, int[] pos)
        {
            for (int i = 0; i < pos.Length; i++)
            {
                ulong src = i < 12 ? key.Item1 : key.Item2;
                pos[i] = (int)((src >> (5 * (i < 12 ? i : i - 12))) & 31) - 1;
            }
        }
    }
}
