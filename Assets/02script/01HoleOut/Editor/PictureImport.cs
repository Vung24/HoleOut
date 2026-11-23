using System.Collections.Generic;
using System.Linq;
using HoleOut.Runtime;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Đổi ảnh thành tranh ô màu: thu nhỏ theo trung bình vùng, chọn màu gần nhất trong bảng chuẩn, gộp bớt màu ít ô.</summary>
    public static class PictureImport
    {
        /// <param name="size">Số ô ở cạnh dài; cạnh còn lại giữ tỉ lệ ảnh.</param>
        /// <param name="maxColors">Số màu tối đa của tranh.</param>
        public static string[] FromTexture(Texture2D tex, int size, int maxColors)
        {
            size = Mathf.Clamp(size, 2, LevelDesign.MaxSide);
            int cols = tex.width >= tex.height ? size : Mathf.Max(2, Mathf.RoundToInt(size * (float)tex.width / tex.height));
            int rows = tex.height > tex.width ? size : Mathf.Max(2, Mathf.RoundToInt(size * (float)tex.height / tex.width));
            var pixels = tex.GetPixels32();
            var codes = PaletteSO.Standard.Select(s => s.code).Distinct().ToArray();
            var colors = codes.Select(PaletteSO.Default).ToArray();

            var cell = new int[rows, cols];
            var counts = new Dictionary<int, int>();
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < cols; x++)
                {
                    // Hàng 0 của tranh là hàng trên cùng; pixel (0,0) của texture ở góc dưới trái.
                    var avg = Average(pixels, tex.width, tex.height,
                        x * tex.width / cols, (x + 1) * tex.width / cols,
                        tex.height - (y + 1) * tex.height / rows, tex.height - y * tex.height / rows);
                    int best = Nearest(avg, colors, null);
                    cell[y, x] = best;
                    counts[best] = counts.TryGetValue(best, out var n) ? n + 1 : 1;
                }

            // Gộp màu ít ô nhất vào màu gần nhất còn lại cho tới khi đủ số màu.
            var alive = new HashSet<int>(counts.Keys);
            while (alive.Count > Mathf.Max(2, maxColors))
            {
                int weakest = alive.OrderBy(c => counts[c]).First();
                alive.Remove(weakest);
                int into = Nearest(colors[weakest], colors, alive);
                counts[into] += counts[weakest];
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        if (cell[y, x] == weakest) cell[y, x] = into;
            }

            var grid = new string[rows];
            for (int y = 0; y < rows; y++)
            {
                var chars = new char[cols];
                for (int x = 0; x < cols; x++) chars[x] = codes[cell[y, x]][0];
                grid[y] = new string(chars);
            }
            return grid;
        }

        /// <summary>Màu trung bình của vùng; điểm ảnh trong suốt coi như nền trắng.</summary>
        static Color Average(Color32[] px, int w, int h, int x0, int x1, int y0, int y1)
        {
            x1 = Mathf.Max(x1, x0 + 1); y1 = Mathf.Max(y1, y0 + 1);
            float r = 0, g = 0, b = 0; int n = 0;
            for (int y = Mathf.Clamp(y0, 0, h - 1); y < Mathf.Min(y1, h); y++)
                for (int x = Mathf.Clamp(x0, 0, w - 1); x < Mathf.Min(x1, w); x++)
                {
                    var p = px[y * w + x];
                    float a = p.a / 255f;
                    r += Mathf.Lerp(1, p.r / 255f, a); g += Mathf.Lerp(1, p.g / 255f, a); b += Mathf.Lerp(1, p.b / 255f, a);
                    n++;
                }
            return n == 0 ? Color.white : new Color(r / n, g / n, b / n);
        }

        static int Nearest(Color c, Color[] palette, HashSet<int> allowed)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                if (allowed != null && !allowed.Contains(i)) continue;
                var p = palette[i];
                // Khoảng cách có trọng số gần với cảm nhận mắt người.
                float dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
                float d = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
