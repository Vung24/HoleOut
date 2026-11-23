namespace HoleOut.Core
{
    /// <summary>
    /// Luật tranh dùng chung cho <see cref="Picture"/> (game) và <see cref="DrainModel"/> (phân tích độ khó).
    /// Ô định danh bằng chỉ số w * columns + col, w = 0 là hàng sát mép thoát; null = ô trống.
    /// </summary>
    public static class PictureRules
    {
        /// <summary>
        /// Nhân vật lộ khi có đường 4 hướng qua ô trống trong cửa sổ [low, high] nối tới hàng low.
        /// Nhân vật ở hàng low lộ trực tiếp. <paramref name="mark"/> và <paramref name="queue"/> là bộ đệm cỡ ≥ số ô.
        /// </summary>
        public static void Flood(string[] color, int columns, int rows, int low, int high,
            bool[] exposed, int[] mark, int[] queue)
        {
            System.Array.Clear(exposed, 0, exposed.Length);
            for (int i = 0; i < mark.Length; i++) mark[i] = -1;
            int head = 0, tail = 0;
            for (int c = 0; c < columns; c++) Visit(color, columns, rows, low, high, c, low, exposed, mark, queue, ref tail);
            while (head < tail)
            {
                int k = queue[head++];
                int c = k % columns, w = k / columns;
                Visit(color, columns, rows, low, high, c - 1, w, exposed, mark, queue, ref tail);
                Visit(color, columns, rows, low, high, c + 1, w, exposed, mark, queue, ref tail);
                Visit(color, columns, rows, low, high, c, w + 1, exposed, mark, queue, ref tail);
                Visit(color, columns, rows, low, high, c, w - 1, exposed, mark, queue, ref tail);
            }
        }

        static void Visit(string[] color, int columns, int rows, int low, int high, int c, int w,
            bool[] exposed, int[] mark, int[] queue, ref int tail)
        {
            if (c < 0 || c >= columns || w < low || w > high || w >= rows) return;
            int k = w * columns + c;
            if (color[k] != null) { exposed[k] = true; return; }
            if (mark[k] >= 0) return;
            mark[k] = 0;
            queue[tail++] = k;
        }

        /// <summary>Hàng gần mép thoát nhất còn nhân vật; -1 nếu tranh trống.</summary>
        public static int LowestOccupiedRow(string[] color, int columns, int rows)
        {
            for (int w = 0; w < rows; w++)
                for (int c = 0; c < columns; c++)
                    if (color[w * columns + c] != null) return w;
            return -1;
        }
    }
}
