using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Ảnh nhỏ của tranh hoàn chỉnh: mỗi ô lưới một điểm ảnh, màu mặt trên của bảng màu chuẩn, ô trống trong suốt.</summary>
    public static class PictureThumbnail
    {
        /// <param name="silhouette">Có giá trị thì mọi ô có màu đều tô bằng màu này (tranh chưa mở).</param>
        public static Texture2D Build(LevelData level, Color32? silhouette = null)
        {
            int cols = level.Columns, rows = level.Rows;
            if (cols == 0 || rows == 0) return null;
            var tex = new Texture2D(cols, rows, TextureFormat.RGBA32, false)
            {
                name = "PictureThumbnail", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[cols * rows];
            for (int r = 0; r < rows; r++)
            {
                string line = level.grid[r];
                // Hàng 0 của dữ liệu ở xa nhất (trên cùng); texture đếm từ dưới lên.
                int y = rows - 1 - r;
                for (int c = 0; c < cols; c++)
                {
                    string code = c < line.Length ? line[c].ToString() : null;
                    pixels[y * cols + c] = code != null && PaletteSO.TryGetSwatch(code, out var s)
                        ? silhouette ?? (Color32)PaletteSO.FromHex(s.body)
                        : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
