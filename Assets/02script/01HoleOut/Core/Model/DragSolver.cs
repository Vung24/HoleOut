using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    public struct DragResult
    {
        /// <summary>Vị trí liên tục hợp lệ của khối (ô lưới, có phần lẻ).</summary>
        public double X, Y;
        /// <summary>Vị trí hiển thị, có thể nhô qua cổng tối đa 0,56 ô.</summary>
        public double VisualX, VisualY;
        public int SnapX, SnapY;
        /// <summary>Cổng khối vừa vượt qua đủ 0,52 ô; null nếu chưa thoát.</summary>
        public GateData Exit;
        public bool Blocked;
        /// <summary>Khối đã lọt cổng cùng màu nhưng khay chờ đầy nên bị chặn lại.</summary>
        public bool DockFull;
    }

    /// <summary>
    /// Kéo tự do 2D trên bàn: khối theo ngón tay, trượt dọc tường/khối khác,
    /// hỗ trợ vào làn ở góc và không xuyên vật cản dù con trỏ dịch xa trong một lần cập nhật.
    /// </summary>
    public static class DragSolver
    {
        public const int BoardWidth = 6;
        public const int BoardHeight = 4;
        public const double ExitThreshold = 0.52;
        public const double ExitExtension = 0.56;

        const double Step = 0.08;
        const double Nudge = 0.08;
        const int MaxIterations = 200;

        public static bool Free(BlockData b, int x, int y, IReadOnlyList<BlockData> blocks)
        {
            if (x < 0 || y < 0 || x + b.w > BoardWidth || y + b.h > BoardHeight) return false;
            for (int i = 0; i < blocks.Count; i++)
            {
                var o = blocks[i];
                if (o.id == b.id) continue;
                if (Overlaps(b, x, y, o)) return false;
            }
            return true;
        }

        public static bool Overlaps(BlockData b, int x, int y, BlockData other)
        {
            int left = Math.Max(x, other.gx), right = Math.Min(x + b.w, other.gx + other.w);
            int top = Math.Max(y, other.gy), bottom = Math.Min(y + b.h, other.gy + other.h);
            if (left >= right || top >= bottom) return false;
            if (b.shape == BlockShape.Rectangle && other.shape == BlockShape.Rectangle) return true;

            for (int row = top; row < bottom; row++)
                for (int col = left; col < right; col++)
                    if (b.ContainsCell(col - x, row - y) && other.ContainsCell(col - other.gx, row - other.gy))
                        return true;
            return false;
        }

        /// <summary>Bề ngang cổng đo vuông góc với hướng thoát.</summary>
        public static bool Fits(BlockData b, GateData d)
        {
            if (d == null) return false;
            int span = d.side == GateSide.Top || d.side == GateSide.Bottom ? b.w : b.h;
            return span <= d.len;
        }

        public static GateData FindGate(BlockData b, double x, double y, IReadOnlyList<GateData> gates, GateSide side)
        {
            const double eps = 1e-6;
            if (b.mystery || b.Frozen) return null;
            bool vertical = side == GateSide.Top || side == GateSide.Bottom;
            double edge = side == GateSide.Top ? y
                : side == GateSide.Bottom ? y + b.h - BoardHeight
                : side == GateSide.Left ? x
                : x + b.w - BoardWidth;
            if (Math.Abs(edge) >= eps) return null;
            double start = vertical ? x : y;
            int span = vertical ? b.w : b.h;
            for (int i = 0; i < gates.Count; i++)
            {
                var d = gates[i];
                if (d.side != side || d.color != b.color || !Fits(b, d)) continue;
                // Khối nhỏ dùng được mọi làn hợp lệ của cổng rộng.
                if (start >= d.start - eps && start + span <= d.start + d.len + eps) return d;
            }
            return null;
        }

        public static bool FreeAt(BlockData b, double x, double y, IReadOnlyList<BlockData> blocks)
        {
            const double e = 1e-4;
            int x0 = (int)Math.Floor(x + e), x1 = (int)Math.Ceiling(x - e);
            int y0 = (int)Math.Floor(y + e), y1 = (int)Math.Ceiling(y - e);
            return Free(b, x0, y0, blocks) && Free(b, x0, y1, blocks) && Free(b, x1, y0, blocks) && Free(b, x1, y1, blocks);
        }

        /// <param name="full">Khay hết chỗ: cổng chặn như tường.</param>
        public static DragResult Drag(BlockData b, IReadOnlyList<BlockData> blocks, IReadOnlyList<GateData> gates,
            double fromX, double fromY, double targetX, double targetY, bool full)
        {
            double x = fromX, y = fromY;
            double tx = Clamp(targetX, 0, BoardWidth - b.w), ty = Clamp(targetY, 0, BoardHeight - b.h);

            for (int i = 0; i < MaxIterations; i++)
            {
                double rx = tx - x, ry = ty - y, dist = Math.Sqrt(rx * rx + ry * ry);
                if (dist < 1e-4) break;
                double k = Math.Min(1, Step / dist), sx = rx * k, sy = ry * k;
                if (FreeAt(b, x + sx, y + sy, blocks)) { x += sx; y += sy; continue; }

                // Khi trượt dọc vật cản, bước trên trục còn đi được không phụ thuộc vào khoảng cách trên trục bị chặn.
                double stepX = Clamp(rx, -Step, Step), stepY = Clamp(ry, -Step, Step);
                bool xFirst = Math.Abs(rx) >= Math.Abs(ry);
                double ax = xFirst ? stepX : 0, ay = xFirst ? 0 : stepY;
                double bx = xFirst ? 0 : stepX, by = xFirst ? stepY : 0;
                if ((ax != 0 || ay != 0) && FreeAt(b, x + ax, y + ay, blocks)) { x += ax; y += ay; continue; }
                if ((bx != 0 || by != 0) && FreeAt(b, x + bx, y + by, blocks)) { x += bx; y += by; continue; }

                // Hỗ trợ góc: đẩy trục phụ về làn gần nhất để bước chính đi qua.
                if (xFirst)
                {
                    double d = RoundJs(y) - y;
                    if (Math.Abs(d) > 1e-4)
                    {
                        double ny = y + Math.Sign(d) * Math.Min(Nudge, Math.Abs(d));
                        if (FreeAt(b, x, ny, blocks)) { y = ny; continue; }
                    }
                }
                else
                {
                    double d = RoundJs(x) - x;
                    if (Math.Abs(d) > 1e-4)
                    {
                        double nx = x + Math.Sign(d) * Math.Min(Nudge, Math.Abs(d));
                        if (FreeAt(b, nx, y, blocks)) { x = nx; continue; }
                    }
                }
                break;
            }

            x = Clamp(x, 0, BoardWidth - b.w);
            y = Clamp(y, 0, BoardHeight - b.h);

            var r = new DragResult
            {
                X = x, Y = y, VisualX = x, VisualY = y,
                SnapX = RoundJs(x), SnapY = RoundJs(y),
                Blocked = Hypot(targetX - x, targetY - y) > 0.6
            };

            if (x <= 1e-6) TryGate(ref r, b, gates, GateSide.Left, -targetX, full);
            if (r.Exit == null && x >= BoardWidth - b.w - 1e-6) TryGate(ref r, b, gates, GateSide.Right, targetX - (BoardWidth - b.w), full);
            if (r.Exit == null && y <= 1e-6) TryGate(ref r, b, gates, GateSide.Top, -targetY, full);
            if (r.Exit == null && y >= BoardHeight - b.h - 1e-6) TryGate(ref r, b, gates, GateSide.Bottom, targetY - (BoardHeight - b.h), full);
            return r;
        }

        static void TryGate(ref DragResult r, BlockData b, IReadOnlyList<GateData> gates, GateSide side, double over, bool full)
        {
            if (over <= 0.02) return;
            bool horizontal = side == GateSide.Left || side == GateSide.Right;
            double x = r.X, y = r.Y;
            double gx = x, gy = y;
            var door = FindGate(b, gx, gy, gates, side);
            // Giữ vị trí kéo tự do khi khối nằm lọt trong cửa; chỉ căn ô nếu lệch nhẹ ở mép.
            if (door == null && Aligned(horizontal ? y : x))
            {
                gx = horizontal ? x : RoundJs(x);
                gy = horizontal ? RoundJs(y) : y;
                door = FindGate(b, gx, gy, gates, side);
            }
            if (door == null || full) { r.Blocked = true; r.DockFull = door != null; return; }
            r.Blocked = false;
            double ext = Math.Min(ExitExtension, over);
            switch (side)
            {
                case GateSide.Left: r.VisualX = x - ext; r.VisualY = gy; break;
                case GateSide.Right: r.VisualX = x + ext; r.VisualY = gy; break;
                case GateSide.Top: r.VisualY = y - ext; r.VisualX = gx; break;
                default: r.VisualY = y + ext; r.VisualX = gx; break;
            }
            if (over >= ExitThreshold) r.Exit = door;
        }

        static bool Aligned(double v) => Math.Abs(v - RoundJs(v)) < 0.22;
        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        /// <summary>Làm tròn nửa lên (giống làm tròn ô khi thả).</summary>
        public static int RoundJs(double v) => (int)Math.Floor(v + 0.5);
    }
}
