using System;

namespace HoleOut.Core
{
    public struct Vec2
    {
        public double X, Z;
        public Vec2(double x, double z) { X = x; Z = z; }
        public static double Distance(Vec2 a, Vec2 b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }

    /// <summary>
    /// Vị trí thế giới dùng để tính quãng chạy của nhân vật. View có thể cung cấp bản riêng
    /// nếu dùng đơn vị khác; mô phỏng chỉ cần khoảng cách.
    /// </summary>
    public interface IBoardLayout
    {
        Vec2 CellToWorld(int col, double visualRow);
        Vec2 DockSlot(int slot, int slotCount);
    }

    /// <summary>
    /// Bố cục mặc định: ô tranh cố định ở mọi màn để nhân vật và hố giữ cùng tỉ lệ.
    /// Khung tranh luôn cỡ <see cref="FrameColumns"/> × <see cref="FrameRows"/> ô; tranh nhỏ hơn
    /// nằm chính giữa khung theo cả hai chiều.
    /// </summary>
    public sealed class DefaultBoardLayout : IBoardLayout
    {
        /// <summary>Cạnh khung tranh (đơn vị thế giới); ô tranh = cạnh khung / số ô mỗi chiều.</summary>
        public const double FrameSize = 7.5;
        public const int FrameColumns = 25;
        public const int FrameRows = 25;
        public const double FixedCell = FrameSize / FrameColumns;
        public const double GridBottomZ = -1.9;
        public const double DockZ = -0.3;

        public readonly double Cell;
        readonly int _cols, _visibleRows;
        readonly double _centerZ;

        public DefaultBoardLayout(int columns, int visibleRows, double pictureScale = 1)
        {
            _cols = columns;
            _visibleRows = visibleRows;
            Cell = FixedCell * pictureScale;
            _centerZ = GridBottomZ - FrameRows * FixedCell / 2;
        }

        public Vec2 CellToWorld(int col, double visualRow) =>
            new Vec2((col - (_cols - 1) / 2.0) * Cell, _centerZ + (visualRow - (_visibleRows - 1) / 2.0) * Cell);

        /// <summary>Hai góc khung tranh, cùng hệ toạ độ với <see cref="CellToWorld"/>; Z lớn là mép sát khay chờ.</summary>
        public static (Vec2 min, Vec2 max) FrameBounds() =>
            (new Vec2(-FrameColumns * FixedCell / 2, GridBottomZ - FrameRows * FixedCell),
             new Vec2(FrameColumns * FixedCell / 2, GridBottomZ));

        public Vec2 DockSlot(int slot, int slotCount)
        {
            double spacing = slotCount <= 4 ? 1.7 : slotCount == 5 ? 1.48 : 1.26;
            return new Vec2((slot - (slotCount - 1) / 2.0) * spacing, DockZ);
        }
    }
}
