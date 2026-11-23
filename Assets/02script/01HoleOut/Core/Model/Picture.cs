using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    /// <summary>
    /// Tranh nhân vật. Ô được định danh bằng (cột, w) với w = khoảng cách từ mép thoát,
    /// w = 0 là hàng cuối của grid[]. Định danh hàng nguồn giữ nguyên khi nhân vật rời tranh.
    /// </summary>
    public sealed class Picture
    {
        public readonly int Columns;
        public readonly int Rows;
        public readonly int VisibleRows;
        public readonly PictureDisplayMode Mode;

        readonly string[] _color;     // null = ô trống
        readonly bool[] _exposed;
        readonly int[] _readyHole;    // -1 = chưa giữ chỗ
        readonly double[] _stand;     // 0 = ngồi, 1 = đứng hẳn

        // Bộ đệm tái sử dụng cho BFS.
        readonly int[] _dist;
        readonly int[] _queue;

        public int RowOffset { get; private set; }
        /// <summary>Vị trí cuộn đang diễn hoạt; bằng RowOffset khi tranh đã ổn định.</summary>
        public double ScrollVisual { get; private set; }
        public int Remaining { get; private set; }
        public int Total { get; }

        public const double ScrollSpeedRowsPerSecond = 15;

        public Picture(LevelData level)
        {
            Rows = level.Rows;
            Columns = level.Columns;
            Mode = level.displayMode;
            VisibleRows = level.EffectiveVisibleRows;
            int n = Rows * Columns;
            _color = new string[n];
            _exposed = new bool[n];
            _readyHole = new int[n];
            _stand = new double[n];
            _dist = new int[n];
            _queue = new int[n];
            for (int r = 0; r < Rows; r++)
            {
                string row = level.grid[r];
                if (row.Length != Columns) throw new ArgumentException($"Hàng {r} dài {row.Length}, cần {Columns}.");
                int w = Rows - 1 - r;
                for (int c = 0; c < Columns; c++)
                {
                    int i = Index(c, w);
                    _color[i] = row[c].ToString();
                    _readyHole[i] = -1;
                }
            }
            Remaining = Total = n;
        }

        public int Index(int col, int w) => w * Columns + col;
        public int SourceRow(int w) => Rows - 1 - w;
        public int WindowLow => RowOffset;
        public int WindowHigh => RowOffset + VisibleRows - 1;
        public bool IsSettled => Math.Abs(ScrollVisual - RowOffset) < 0.02;

        public bool Occupied(int col, int w) => InGrid(col, w) && _color[Index(col, w)] != null;
        public string ColorAt(int col, int w) => InGrid(col, w) ? _color[Index(col, w)] : null;
        public bool IsExposed(int col, int w) => InGrid(col, w) && _exposed[Index(col, w)];
        public int ReadyHole(int col, int w) => _readyHole[Index(col, w)];
        public void SetReadyHole(int col, int w, int holeId) => _readyHole[Index(col, w)] = holeId;
        public double Stand(int col, int w) => _stand[Index(col, w)];

        /// <summary>Nhân vật có thể rời chỗ: đã lộ và tranh không còn đang trượt.</summary>
        public bool CanLeave(int col, int w) => IsExposed(col, w) && IsSettled;

        bool InGrid(int col, int w) => col >= 0 && col < Columns && w >= 0 && w < Rows;
        bool InWindow(int col, int w) => col >= 0 && col < Columns && w >= WindowLow && w <= WindowHigh && w < Rows;

        public void Remove(int col, int w)
        {
            int i = Index(col, w);
            if (_color[i] == null) return;
            _color[i] = null;
            _exposed[i] = false;
            _readyHole[i] = -1;
            _stand[i] = 0;
            Remaining--;
        }

        /// <summary>Khoảng cách nhỏ nhất từ mép thoát còn nhân vật; -1 nếu tranh trống.</summary>
        public int LowestOccupiedW() => PictureRules.LowestOccupiedRow(_color, Columns, Rows);

        /// <summary>
        /// Tiến cửa sổ tới hàng gần nhất còn nhân vật (có thể vượt nhiều hàng sạch).
        /// Chỉ áp dụng cho WindowedScroll.
        /// </summary>
        public bool AdvanceWindow()
        {
            if (Mode != PictureDisplayMode.WindowedScroll) return false;
            int low = LowestOccupiedW();
            if (low <= RowOffset) return false;
            RowOffset = low;
            return true;
        }

        public void AnimateScroll(double dt)
        {
            if (ScrollVisual < RowOffset) ScrollVisual = Math.Min(RowOffset, ScrollVisual + dt * ScrollSpeedRowsPerSecond);
            else ScrollVisual = RowOffset;
        }

        public void UpdateStand(double dt)
        {
            bool settled = IsSettled;
            for (int i = 0; i < _color.Length; i++)
            {
                if (_color[i] == null) continue;
                _stand[i] = _exposed[i] && settled ? Math.Min(1, _stand[i] + dt * 4.5) : Math.Max(0, _stand[i] - dt * 7);
            }
        }

        /// <summary>
        /// Nhân vật lộ khi có đường 4 hướng qua ô trống trong cửa sổ nối tới mép dưới.
        /// Nhân vật ở hàng dưới cùng của cửa sổ lộ trực tiếp.
        /// </summary>
        public void UpdateExposure() =>
            PictureRules.Flood(_color, Columns, Rows, WindowLow, WindowHigh, _exposed, _dist, _queue);

        /// <summary>
        /// Đường ngắn nhất qua ô trống trong cửa sổ từ nhân vật (col, w) ra mép dưới.
        /// Trả về các ô (không gồm ô xuất phát) theo thứ tự đi; null nếu không có đường.
        /// </summary>
        public List<(int col, int w)> FindExitPath(int col, int w)
        {
            int low = WindowLow;
            for (int i = 0; i < _dist.Length; i++) _dist[i] = -1;
            int head = 0, tail = 0;
            for (int c = 0; c < Columns; c++)
                if (IsFreeForPath(c, low)) { int k = Index(c, low); _dist[k] = 0; _queue[tail++] = k; }
            while (head < tail)
            {
                int k = _queue[head++];
                int c = k % Columns, ww = k / Columns, d = _dist[k];
                TryEnqueue(c, ww - 1, d, ref tail);
                TryEnqueue(c - 1, ww, d, ref tail);
                TryEnqueue(c + 1, ww, d, ref tail);
                TryEnqueue(c, ww + 1, d, ref tail);
            }

            var cells = new List<(int, int)>();
            int cc = col, cw = w;
            if (cw > low)
            {
                // Bước sang ô trống kề gần lối thoát nhất (ưu tiên thẳng xuống).
                int best = -1, bestD = int.MaxValue;
                ConsiderStep(cc, cw - 1, ref best, ref bestD);
                ConsiderStep(cc - 1, cw, ref best, ref bestD);
                ConsiderStep(cc + 1, cw, ref best, ref bestD);
                ConsiderStep(cc, cw + 1, ref best, ref bestD);
                if (best < 0) return null;
                cc = best % Columns; cw = best / Columns;
                cells.Add((cc, cw));
                while (_dist[Index(cc, cw)] > 0)
                {
                    int want = _dist[Index(cc, cw)] - 1;
                    if (DistIs(cc, cw - 1, want)) cw--;
                    else if (DistIs(cc - 1, cw, want)) cc--;
                    else if (DistIs(cc + 1, cw, want)) cc++;
                    else if (DistIs(cc, cw + 1, want)) cw++;
                    else break;
                    cells.Add((cc, cw));
                }
            }
            return cells;
        }

        /// <summary>Kiểm tra đoạn thẳng từ tâm ô xuất phát qua mép dưới mà không cắt ô có nhân vật.</summary>
        internal bool CanRunStraight(int col, int w, double endCol, double endW)
        {
            double bottom = WindowLow - 0.5;
            if (endW >= bottom) return false;

            double slope = (endCol - col) / (endW - w);
            double bottomCol = col + (bottom - w) * slope;
            if (bottomCol < -0.5 || bottomCol > Columns - 0.5) return false;

            // Xét toàn bộ khoảng cột đoạn thẳng đi qua trong từng hàng, kể cả khi chạm góc ô.
            const double tolerance = 1e-9;
            double entryCol = col;
            for (int row = w; row >= WindowLow; row--)
            {
                double exitCol = col + (row - 0.5 - w) * slope;
                int first = Math.Max(0, (int)Math.Ceiling(Math.Min(entryCol, exitCol) - 0.5 - tolerance));
                int last = Math.Min(Columns - 1, (int)Math.Floor(Math.Max(entryCol, exitCol) + 0.5 + tolerance));
                for (int c = first; c <= last; c++)
                    if ((c != col || row != w) && Occupied(c, row)) return false;
                entryCol = exitCol;
            }
            return true;
        }

        bool IsFreeForPath(int c, int w) => InWindow(c, w) && _color[Index(c, w)] == null;

        void TryEnqueue(int c, int w, int d, ref int tail)
        {
            if (!IsFreeForPath(c, w)) return;
            int k = Index(c, w);
            if (_dist[k] >= 0) return;
            _dist[k] = d + 1;
            _queue[tail++] = k;
        }

        void ConsiderStep(int c, int w, ref int best, ref int bestD)
        {
            if (!InWindow(c, w)) return;
            int d = _dist[Index(c, w)];
            if (d >= 0 && d < bestD) { best = Index(c, w); bestD = d; }
        }

        bool DistIs(int c, int w, int d) => InWindow(c, w) && _dist[Index(c, w)] == d;
    }
}
