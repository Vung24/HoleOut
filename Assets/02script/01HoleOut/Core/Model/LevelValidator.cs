using System.Collections.Generic;

namespace HoleOut.Core
{
    /// <summary>
    /// Kiểm tra dữ liệu màn. Đạt validation chưa chứng minh màn có lời giải;
    /// khả năng giải được kiểm bằng <see cref="SolutionReplayer"/>.
    /// </summary>
    public static class LevelValidator
    {
        public static List<string> Validate(LevelData level)
        {
            var errors = new List<string>();
            if (level.grid == null || level.grid.Length == 0) { errors.Add("Tranh trống."); return errors; }

            int cols = level.grid[0].Length;
            var pixels = new Dictionary<string, int>();
            for (int r = 0; r < level.grid.Length; r++)
            {
                if (level.grid[r].Length != cols) errors.Add($"Hàng {r} dài {level.grid[r].Length}, cần {cols}.");
                foreach (char ch in level.grid[r])
                {
                    if (char.IsWhiteSpace(ch) || ch == '.') { errors.Add($"Hàng {r} có ô trống '{ch}'."); continue; }
                    string k = ch.ToString();
                    pixels[k] = pixels.TryGetValue(k, out var n) ? n + 1 : 1;
                }
            }

            if (level.displayMode == PictureDisplayMode.WindowedScroll && level.visibleRows <= 0)
                errors.Add("visibleRows phải lớn hơn 0.");
            if (level.pictureScale <= 0 || float.IsNaN(level.pictureScale) || float.IsInfinity(level.pictureScale))
                errors.Add("pictureScale phải là số hữu hạn lớn hơn 0.");
            if (cols * level.pictureScale > DefaultBoardLayout.FrameColumns)
                errors.Add($"Tranh rộng {cols} cột, khung tranh chỉ có {DefaultBoardLayout.FrameColumns} cột.");
            if (level.EffectiveVisibleRows * level.pictureScale > DefaultBoardLayout.FrameRows)
                errors.Add($"Tranh hiện {level.EffectiveVisibleRows} hàng, khung tranh chỉ có {DefaultBoardLayout.FrameRows} hàng.");
            if (level.dockSlots < 1 || level.dockSlots > Dock.MaxSlotsLimit)
                errors.Add($"dockSlots {level.dockSlots} ngoài khoảng 1..{Dock.MaxSlotsLimit}.");
            if (level.waves == null || level.waves.Count == 0 || level.waves[0].blocks.Count == 0)
                errors.Add("Không có đợt khối đầu tiên.");

            var capacity = new Dictionary<string, int>();
            var ids = new HashSet<int>();
            for (int wi = 0; wi < (level.waves?.Count ?? 0); wi++)
            {
                var blocks = level.waves[wi].blocks;
                bool hasFreeBlock = blocks.Count == 0;
                for (int i = 0; i < blocks.Count; i++)
                {
                    var b = blocks[i];
                    if (!ids.Add(b.id)) errors.Add($"ID khối {b.id} bị trùng.");
                    if (b.ice < 0 || b.ice >= blocks.Count)
                        errors.Add($"Khối {b.id} có {b.ice} lớp băng; đợt {wi + 1} có {blocks.Count} khối nên cần 0..{blocks.Count - 1}.");
                    hasFreeBlock |= b.ice == 0;
                    if (b.w < 1 || b.h < 1 || b.gx < 0 || b.gy < 0 || b.gx + b.w > DragSolver.BoardWidth || b.gy + b.h > DragSolver.BoardHeight)
                        errors.Add($"Khối {b.id} ở ({b.gx},{b.gy}) cỡ {b.w}×{b.h} nằm ngoài bàn.");
                    var frame = BlockData.FrameOf(b.shape);
                    if (frame.HasValue && (b.w != frame.Value.w || b.h != frame.Value.h))
                        errors.Add($"Khối {b.shape} {b.id} cần khung {frame.Value.w}×{frame.Value.h}.");
                    if (b.capacity <= 0) errors.Add($"Khối {b.id} có sức chứa {b.capacity}.");
                    if (string.IsNullOrEmpty(b.color) || !pixels.ContainsKey(b.color)) errors.Add($"Khối {b.id} màu '{b.color}' không có trong tranh.");
                    else capacity[b.color] = (capacity.TryGetValue(b.color, out var c) ? c : 0) + b.capacity;

                    for (int j = 0; j < i; j++)
                    {
                        var o = blocks[j];
                        if (DragSolver.Overlaps(b, b.gx, b.gy, o))
                            errors.Add($"Đợt {wi + 1}: khối {b.id} chồng khối {o.id}.");
                    }

                    bool hasGate = false;
                    foreach (var g in level.gates) hasGate |= g.color == b.color && DragSolver.Fits(b, g);
                    if (!hasGate) errors.Add($"Khối {b.id} ({b.color} {b.w}×{b.h}) không có cổng cùng màu đủ rộng.");
                }
                if (!hasFreeBlock) errors.Add($"Đợt {wi + 1}: mọi khối đều đóng băng, không khối nào kéo được đầu tiên.");
            }

            foreach (var kv in pixels)
            {
                capacity.TryGetValue(kv.Key, out var cap);
                if (cap != kv.Value) errors.Add($"Màu {kv.Key}: tranh có {kv.Value} nhân vật, tổng sức chứa {cap}.");
            }

            foreach (var g in level.gates)
            {
                int limit = g.side == GateSide.Top || g.side == GateSide.Bottom ? DragSolver.BoardWidth : DragSolver.BoardHeight;
                if (g.len < 1 || g.start < 0 || g.start + g.len > limit) errors.Add($"Cổng {g.color} {g.side} {g.start}+{g.len} nằm ngoài cạnh.");
            }
            return errors;
        }
    }
}
