using HoleOut.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Thao tác kéo khối trên bàn: chạm chọn khối, vượt ngưỡng thì bắt đầu kéo, thả thì khớp ô.
    /// Luật di chuyển và thoát cổng nằm ở <see cref="GameSimulation"/>; lớp này chỉ đổi con trỏ sang tọa độ ô.
    /// </summary>
    public sealed class BoardInput
    {
        /// <summary>Ngưỡng bắt đầu kéo, tính theo ô.</summary>
        const float DragStartThreshold = 0.085f;
        /// <summary>Độ cao mặt phẳng chạm của bàn (mặt trên khối).</summary>
        const float BoardSurfaceY = 0.35f;

        readonly Camera _camera;
        int _pressBlock = -1;
        bool _dragStarted;
        Vector2 _pressCell;
        Vector2 _blockOrigin;
        Vector2 _chase;
        bool _hasChase;

        readonly float _followSpeed;

        public BoardInput(Camera camera, float followSpeed)
        {
            _camera = camera;
            _followSpeed = Mathf.Max(1f, followSpeed);
        }

        public bool IsPressing => _pressBlock >= 0;

        public void Reset()
        {
            _pressBlock = -1;
            _dragStarted = false;
            _hasChase = false;
        }

        /// <summary>Hủy lượt kéo đang dở (khối về chỗ cũ).</summary>
        public void Cancel(GameSimulation sim)
        {
            if (_pressBlock >= 0) sim?.CancelDrag();
            Reset();
        }

        /// <summary>Xử lý một frame kéo. Trả về true nếu lượt kéo vừa kết thúc (thả tay hoặc khối thoát qua cổng).</summary>
        public bool Tick(GameSimulation sim, GameView view, Pointer pointer, Vector2 screen, bool overUi)
        {
            if (pointer.press.wasPressedThisFrame && !overUi)
            {
                if (!ScreenToBoard(screen, out var cell)) return false;
                var b = BlockAt(sim, cell);
                if (b != null && sim.BeginDrag(b.id))
                {
                    _pressBlock = b.id;
                    _pressCell = cell;
                    _blockOrigin = new Vector2(b.gx, b.gy);
                    _dragStarted = false;
                }
            }

            if (_pressBlock >= 0 && pointer.press.isPressed && ScreenToBoard(screen, out var now))
            {
                var d = now - _pressCell;
                if (!_dragStarted && Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)) > DragStartThreshold) _dragStarted = true;
                if (_dragStarted)
                {
                    // Khối đuổi theo tay với tốc độ giới hạn: kéo thường vẫn bám tay,
                    // còn khi vừa hết vướng thì trượt tới điểm tay thay vì nhảy ngay.
                    var block = sim.Board.Find(_pressBlock);
                    var current = new Vector2((float)sim.DragX, (float)sim.DragY);
                    // Khối bị kẹp ở mép bàn: giữ phần điểm kéo đã vượt ra ngoài để đủ lực đẩy qua cổng.
                    if (block != null && _hasChase)
                    {
                        float maxX = DragSolver.BoardWidth - block.w, maxY = DragSolver.BoardHeight - block.h;
                        if ((current.x <= 0f && _chase.x < 0f) || (current.x >= maxX && _chase.x > maxX)) current.x = _chase.x;
                        if ((current.y <= 0f && _chase.y < 0f) || (current.y >= maxY && _chase.y > maxY)) current.y = _chase.y;
                    }
                    var target = Vector2.MoveTowards(current, _blockOrigin + d, _followSpeed * Time.deltaTime);
                    _chase = target; _hasChase = true;
                    var r = sim.DragTo(target.x, target.y);
                    if (sim.DraggingBlockId != _pressBlock)
                    {
                        // Khối đã thoát qua cổng.
                        if (block != null) view.NoteLaunch(block, r);
                        Reset();
                        return true;
                    }
                }
            }

            if (_pressBlock >= 0 && pointer.press.wasReleasedThisFrame)
            {
                if (_dragStarted) sim.EndDrag(); else sim.CancelDrag();
                Reset();
                return true;
            }
            return false;
        }

        /// <summary>Khối chứa ô <paramref name="cell"/> (tọa độ ô thực), null nếu chạm vào chỗ trống.</summary>
        public static BlockData BlockAt(GameSimulation sim, Vector2 cell)
        {
            foreach (var b in sim.Board.Blocks)
                if (b.ContainsCell(cell.x - b.gx, cell.y - b.gy)) return b;
            return null;
        }

        public bool ScreenToBoard(Vector2 screen, out Vector2 cell)
        {
            cell = default;
            var ray = _camera.ScreenPointToRay(screen);
            var plane = BoardSpace.BoardPlane(BoardSurfaceY);
            if (!plane.Raycast(ray, out float enter)) return false;
            cell = BoardSpace.WorldToBoard(ray.GetPoint(enter));
            return true;
        }
    }
}
