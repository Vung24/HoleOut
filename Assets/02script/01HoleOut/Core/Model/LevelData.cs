using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    public enum GateSide { Top, Bottom, Left, Right }

    public enum PictureDisplayMode { WindowedScroll, FullPicture }
    /// <summary>L: khung 2×2 thiếu ô trên phải. T: khung 3×2, hàng dưới đủ và ô giữa hàng trên. Plus: khung 3×3 hình dấu cộng.</summary>
    public enum BlockShape { Rectangle, L, T, Plus }

    /// <summary>Một khối trên bàn 6×4. Màu là mã một ký tự trong tranh.</summary>
    [Serializable]
    public class BlockData
    {
        public int id;
        public int gx;
        public int gy;
        public int w = 1;
        public int h = 1;
        public BlockShape shape;
        public string color;
        public int capacity;
        public bool mystery;
        /// <summary>Số khối khác còn phải rời bàn trước khi khối này kéo được. 0 = không đóng băng.</summary>
        public int ice;

        public bool Frozen => ice > 0;

        public bool ContainsCell(double localX, double localY) =>
            localX >= 0 && localX < w && localY >= 0 && localY < h && ShapeContains((int)localX, (int)localY);

        bool ShapeContains(int x, int y)
        {
            switch (shape)
            {
                case BlockShape.L: return x < 1 || y >= 1;
                case BlockShape.T: return y == 1 || x == 1;
                case BlockShape.Plus: return x == 1 || y == 1;
                default: return true;
            }
        }

        /// <summary>Khung w×h bắt buộc của hình đặc biệt; Rectangle trả về null.</summary>
        public static (int w, int h)? FrameOf(BlockShape shape) => shape switch
        {
            BlockShape.L => (2, 2),
            BlockShape.T => (3, 2),
            BlockShape.Plus => (3, 3),
            _ => ((int, int)?)null,
        };

        public BlockData Copy() => (BlockData)MemberwiseClone();
    }

    /// <summary>Cổng trên biên bàn. start/len đo theo cạnh mà cổng nằm trên.</summary>
    [Serializable]
    public class GateData
    {
        public string color;
        public GateSide side;
        public int start;
        public int len = 1;
    }

    [Serializable]
    public class WaveData
    {
        public List<BlockData> blocks = new List<BlockData>();
    }

    /// <summary>Dữ liệu đầu vào bất biến của một màn. waves[0] là đợt đầu.</summary>
    [Serializable]
    public class LevelData
    {
        public string levelId;
        public string[] grid;
        public PictureDisplayMode displayMode = PictureDisplayMode.WindowedScroll;
        public int visibleRows = 10;
        public float pictureScale = 1f;
        public int dockSlots = 5;
        public List<WaveData> waves = new List<WaveData>();
        public List<GateData> gates = new List<GateData>();

        public int Rows => grid?.Length ?? 0;
        public int Columns => Rows > 0 ? grid[0].Length : 0;

        /// <summary>Số hàng được xét/vẽ: FullPicture luôn bao phủ toàn tranh.</summary>
        public int EffectiveVisibleRows =>
            displayMode == PictureDisplayMode.FullPicture ? Rows : Math.Max(1, Math.Min(visibleRows, Rows));
    }
}
