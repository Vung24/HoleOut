using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    /// <summary>Thứ tự duyệt các ô lộ khi chia nhân vật cho hố. RowCol giống game.</summary>
    public enum CellOrder { RowCol, RowColReversed, ColumnReversedRow, ColumnRow }

    public enum DrainStatus { Playing, Won, Jammed }

    public struct DockHole
    {
        /// <summary>Chỉ số khối trong <see cref="DrainModel.Blocks"/>.</summary>
        public int Block;
        public int Remaining;
    }

    /// <summary>Khóa trạng thái để ghi nhớ khi tìm kiếm.</summary>
    public readonly struct DrainKey : IEquatable<DrainKey>
    {
        readonly ulong _launched, _cells1, _cells2, _dock;
        public DrainKey(ulong launched, ulong cells1, ulong cells2, ulong dock)
        { _launched = launched; _cells1 = cells1; _cells2 = cells2; _dock = dock; }
        public bool Equals(DrainKey o) => _launched == o._launched && _cells1 == o._cells1 && _cells2 == o._cells2 && _dock == o._dock;
        public override bool Equals(object obj) => obj is DrainKey k && Equals(k);
        public override int GetHashCode() => (int)(_cells1 ^ (_cells1 >> 32) ^ _launched * 31 ^ _dock * 17);
    }

    /// <summary>Một thời điểm "đã lắng" của màn: tranh, khay và các khối đã đưa ra.</summary>
    public sealed class DrainState
    {
        internal string[] Cells;
        internal ulong Hash1, Hash2;
        public int RowOffset;
        public readonly List<DockHole> Dock = new List<DockHole>();
        /// <summary>Bit i = khối i đã ra khỏi bàn.</summary>
        public ulong Launched;
        public int LaunchCount;
        public int Remaining;
        public DrainStatus Status;

        public DrainState Clone()
        {
            var s = new DrainState
            {
                Cells = (string[])Cells.Clone(), Hash1 = Hash1, Hash2 = Hash2, RowOffset = RowOffset,
                Launched = Launched, LaunchCount = LaunchCount, Remaining = Remaining, Status = Status
            };
            s.Dock.AddRange(Dock);
            return s;
        }

        public bool IsLaunched(int block) => (Launched & (1UL << block)) != 0;
        public string CellColor(int index) => Cells[index];
    }

    /// <summary>
    /// Mô hình nhanh của luật tranh + khay, bỏ qua thời gian và đường đi trên bàn. Dùng để tìm kiếm
    /// và đo độ khó; mỗi bước "đưa một khối ra" rồi chạy tới khi tranh và khay lắng hẳn.
    ///
    /// Khớp <see cref="GameSimulation"/>:
    ///  - ô lộ theo <see cref="PictureRules.Flood"/> trong cửa sổ; cửa sổ tiến ngay khi hàng thấp nhất sạch;
    ///  - ô lộ được giữ chỗ cho hố cùng màu lên khay sớm nhất còn chỗ; từng ô đã giữ chỗ xuất phát
    ///    theo thứ tự <see cref="CellOrder"/>, ô mới lộ được giữ chỗ ngay (xem <see cref="Settle"/>);
    ///  - lắng mà khay đầy (và tranh chưa sạch) là kẹt; hết khối mà tranh chưa sạch cũng là thua.
    /// Bỏ qua thời gian (nhân vật đứng dậy, giới hạn người chạy, lịch đáp). DrainModelTests đối chiếu
    /// kết quả và số ô còn lại với mô phỏng thật trên bộ màn test.
    /// Kết quả cuối luôn phải phát lại qua <see cref="SolutionReplayer"/>.
    /// </summary>
    public sealed class DrainModel
    {
        public const int MaxBlocks = 64;

        public readonly LevelData Level;
        public readonly CellOrder Order;
        public readonly int Columns, Rows, VisibleRows, Slots;
        public readonly bool Windowed;
        /// <summary>Mọi khối của mọi đợt, theo thứ tự đợt rồi thứ tự trong đợt.</summary>
        public readonly List<BlockData> Blocks = new List<BlockData>();
        public readonly List<int> BlockWave = new List<int>();
        public int BlockCount => Blocks.Count;
        public int WaveCount { get; }

        readonly string[] _initial;
        readonly ulong[] _z1, _z2;
        readonly bool[] _exposed;
        readonly int[] _mark, _queue;
        // Bộ đệm giữ chỗ trong một lần lắng: ô → khối của hố đang giữ, và số chỗ đã giữ theo khối.
        readonly int[] _hold, _held, _scan;
        readonly Dictionary<string, string> _colors = new Dictionary<string, string>();

        public DrainModel(LevelData level, CellOrder order = CellOrder.RowCol)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Order = order;
            Rows = level.Rows;
            Columns = level.Columns;
            VisibleRows = level.EffectiveVisibleRows;
            Windowed = level.displayMode == PictureDisplayMode.WindowedScroll;
            Slots = level.dockSlots > 0 ? level.dockSlots : 5;

            int n = Rows * Columns;
            _initial = new string[n];
            for (int r = 0; r < Rows; r++)
            {
                string row = level.grid[r];
                if (row.Length != Columns) throw new ArgumentException($"Hàng {r} dài {row.Length}, cần {Columns}.");
                int w = Rows - 1 - r;
                for (int c = 0; c < Columns; c++) _initial[w * Columns + c] = Canonical(row[c].ToString());
            }

            for (int wi = 0; wi < level.waves.Count; wi++)
                foreach (var b in level.waves[wi].blocks)
                {
                    var copy = b.Copy();
                    copy.color = Canonical(copy.color);
                    Blocks.Add(copy);
                    BlockWave.Add(wi);
                }
            WaveCount = level.waves.Count;
            if (Blocks.Count > MaxBlocks) throw new ArgumentException($"Màn có {Blocks.Count} khối, mô hình hỗ trợ tối đa {MaxBlocks}.");

            _z1 = new ulong[n];
            _z2 = new ulong[n];
            ulong seed = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < n; i++) { _z1[i] = SplitMix(ref seed); _z2[i] = SplitMix(ref seed); }
            _exposed = new bool[n];
            _mark = new int[n];
            _queue = new int[n];
            _hold = new int[n];
            _held = new int[Blocks.Count];
            _scan = ScanOrder(order, Columns, Rows);
        }

        string Canonical(string color)
        {
            if (color == null) return null;
            if (!_colors.TryGetValue(color, out var c)) _colors[color] = c = color;
            return c;
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int IndexOfBlockId(int id)
        {
            for (int i = 0; i < Blocks.Count; i++) if (Blocks[i].id == id) return i;
            return -1;
        }

        public DrainState Initial()
        {
            var s = new DrainState { Cells = (string[])_initial.Clone(), Remaining = _initial.Length };
            Settle(s);
            return s;
        }

        /// <summary>Đợt đang trên bàn: đợt thấp nhất còn khối chưa ra; -1 nếu hết khối.</summary>
        public int CurrentWave(DrainState s)
        {
            for (int i = 0; i < Blocks.Count; i++) if (!s.IsLaunched(i)) return BlockWave[i];
            return -1;
        }

        /// <summary>Các khối người chơi có thể đưa ra lúc này (chỉ xét số, không xét đường đi).</summary>
        public void Choices(DrainState s, List<int> into)
        {
            into.Clear();
            if (s.Status != DrainStatus.Playing || s.Dock.Count >= Slots) return;
            int wave = CurrentWave(s);
            for (int i = 0; i < Blocks.Count; i++)
                if (BlockWave[i] == wave && !s.IsLaunched(i)) into.Add(i);
        }

        public bool CanLaunch(DrainState s, int block) =>
            s.Status == DrainStatus.Playing && s.Dock.Count < Slots && block >= 0 && block < Blocks.Count &&
            !s.IsLaunched(block) && BlockWave[block] == CurrentWave(s);

        public DrainState Launch(DrainState s, int block)
        {
            var next = s.Clone();
            LaunchInPlace(next, block);
            return next;
        }

        public void LaunchInPlace(DrainState s, int block)
        {
            if (!CanLaunch(s, block)) throw new InvalidOperationException($"Không đưa được khối {block} ra lúc này.");
            s.Launched |= 1UL << block;
            s.LaunchCount++;
            s.Dock.Add(new DockHole { Block = block, Remaining = Blocks[block].capacity });
            Settle(s);
        }

        /// <summary>Chơi một thứ tự khối từ đầu. Trả về trạng thái cuối (dừng sớm khi không đưa ra được).</summary>
        public DrainState Play(IReadOnlyList<int> order)
        {
            var s = Initial();
            foreach (var b in order)
            {
                if (!CanLaunch(s, b)) { if (s.Status == DrainStatus.Playing) s.Status = DrainStatus.Jammed; break; }
                LaunchInPlace(s, b);
            }
            return s;
        }

        public DrainKey Key(DrainState s)
        {
            ulong dock = 1469598103934665603UL;
            foreach (var h in s.Dock) dock = (dock ^ (ulong)(h.Block * 4099 + h.Remaining + 1)) * 1099511628211UL;
            return new DrainKey(s.Launched, s.Hash1, s.Hash2, dock);
        }

        /// <summary>Đếm số ô lộ theo màu trong cửa sổ hiện tại.</summary>
        public void CountExposed(DrainState s, Dictionary<string, int> into)
        {
            into.Clear();
            FloodState(s);
            for (int i = 0; i < _exposed.Length; i++)
                if (_exposed[i]) into[s.Cells[i]] = into.TryGetValue(s.Cells[i], out var n) ? n + 1 : 1;
        }

        void FloodState(DrainState s) =>
            PictureRules.Flood(s.Cells, Columns, Rows, s.RowOffset, s.RowOffset + VisibleRows - 1, _exposed, _mark, _queue);

        /// <summary>
        /// Chạy tới khi lắng, giống vòng giữ chỗ/xuất phát của game: ô lộ được giữ chỗ cho hố cùng màu lên khay
        /// sớm nhất còn chỗ (chỗ đã giữ được giữ nguyên); mỗi lần một ô đã giữ chỗ xuất phát theo thứ tự duyệt,
        /// ô mới lộ được giữ chỗ ngay nếu hố còn chỗ. Cửa sổ trượt thì bỏ mọi chỗ đang giữ.
        /// </summary>
        void Settle(DrainState s)
        {
            for (int i = 0; i < _hold.Length; i++) _hold[i] = -1;
            Array.Clear(_held, 0, _held.Length);

            while (s.Remaining > 0)
            {
                if (Windowed)
                {
                    int lowest = PictureRules.LowestOccupiedRow(s.Cells, Columns, Rows);
                    if (lowest > s.RowOffset)
                    {
                        s.RowOffset = lowest;
                        for (int i = 0; i < _hold.Length; i++) _hold[i] = -1;
                        Array.Clear(_held, 0, _held.Length);
                    }
                }
                if (s.Dock.Count == 0) break;
                FloodState(s);

                int low = s.RowOffset * Columns, high = Math.Min(Rows, s.RowOffset + VisibleRows) * Columns;
                int next = -1;
                foreach (int k in _scan)
                {
                    if (k < low || k >= high || !_exposed[k]) continue;
                    if (_hold[k] < 0) Reserve(s, k);
                    if (_hold[k] >= 0 && next < 0) next = k;
                }
                if (next < 0) break;
                Dispatch(s, next);
            }

            if (s.Remaining == 0) { s.Status = DrainStatus.Won; s.Dock.Clear(); }
            else if (s.Dock.Count >= Slots || s.LaunchCount == Blocks.Count) s.Status = DrainStatus.Jammed;
            else s.Status = DrainStatus.Playing;
        }

        void Reserve(DrainState s, int k)
        {
            string color = s.Cells[k];
            for (int i = 0; i < s.Dock.Count; i++)
            {
                var h = s.Dock[i];
                if (!ReferenceEquals(Blocks[h.Block].color, color) || h.Remaining - _held[h.Block] <= 0) continue;
                _hold[k] = h.Block;
                _held[h.Block]++;
                return;
            }
        }

        void Dispatch(DrainState s, int k)
        {
            int block = _hold[k];
            _hold[k] = -1;
            _held[block]--;
            for (int i = 0; i < s.Dock.Count; i++)
            {
                var h = s.Dock[i];
                if (h.Block != block) continue;
                h.Remaining--;
                if (h.Remaining <= 0) s.Dock.RemoveAt(i);
                else s.Dock[i] = h;
                break;
            }
            s.Cells[k] = null;
            s.Hash1 ^= _z1[k];
            s.Hash2 ^= _z2[k];
            s.Remaining--;
        }

        static int[] ScanOrder(CellOrder order, int columns, int rows)
        {
            var scan = new int[columns * rows];
            int n = 0;
            switch (order)
            {
                case CellOrder.RowCol:
                    for (int w = 0; w < rows; w++) for (int c = 0; c < columns; c++) scan[n++] = w * columns + c;
                    break;
                case CellOrder.RowColReversed:
                    for (int w = 0; w < rows; w++) for (int c = columns - 1; c >= 0; c--) scan[n++] = w * columns + c;
                    break;
                case CellOrder.ColumnReversedRow:
                    for (int c = columns - 1; c >= 0; c--) for (int w = 0; w < rows; w++) scan[n++] = w * columns + c;
                    break;
                default:
                    for (int c = 0; c < columns; c++) for (int w = 0; w < rows; w++) scan[n++] = w * columns + c;
                    break;
            }
            return scan;
        }
    }
}
