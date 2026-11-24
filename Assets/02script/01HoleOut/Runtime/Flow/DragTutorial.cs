using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Hướng dẫn kéo khối ở màn có <see cref="LevelSO.tutorialFirstDrag"/>: phủ tối màn hình, chỉ khối và cổng cùng màu sáng,
    /// ngón tay diễn mẫu thao tác kéo khối ra cổng, kèm lời nhắn; kết thúc khi khối đầu tiên ra cổng.
    /// Nước đi lấy từ lời giải đã kiểm tra của màn; màn chưa có lời giải thì chọn khối đầu tiên có cổng cùng màu.
    /// </summary>
    public sealed class DragTutorial
    {
        const string Message = "Kéo khối tới cổng cùng màu";
        // Điểm cuối của ngón tay nằm ngoài mép bàn, ngay trên cổng (ô).
        const float ExitReach = 0.5f;

        readonly GameHud _hud;
        readonly Camera _camera;
        readonly List<GridPoint> _path = new List<GridPoint>();
        readonly List<Vector2> _screen = new List<Vector2>();
        readonly List<Renderer> _focus = new List<Renderer>();
        readonly FocusCapture _capture;

        GameSimulation _sim;
        GameView _view;
        int _blockId = -1;
        GateSide? _door;

        /// <param name="focusLayer">Layer trống dùng tạm khi chụp khối/cổng được làm sáng; -1 thì không phủ tối.</param>
        public DragTutorial(GameHud hud, Camera camera, int focusLayer)
        {
            _hud = hud;
            _camera = camera;
            _capture = new FocusCapture(camera, focusLayer);
        }

        public bool Active => _blockId >= 0;

        public void Begin(LevelSO level, GameSimulation sim, GameView view)
        {
            End();
            _sim = sim;
            _view = view;
            if (!level.tutorialFirstDrag || !PickMove(level.solution)) return;
            _view.SetTutorialFocus(_blockId);
        }

        /// <summary>Gọi mỗi frame sau khi mô phỏng chạy, trước khi xoá sự kiện.</summary>
        public void Tick(IReadOnlyList<GameEvent> events, bool inputOpen)
        {
            if (!Active) return;
            foreach (var e in events)
                if (e.Type == GameEventType.BlockExited) { End(); return; }

            var block = _sim.Board.Find(_blockId);
            if (block == null) { End(); return; }
            // Cảnh mở màn hoặc bảng che bàn: tạm ẩn toàn bộ hướng dẫn.
            if (!inputOpen) { _hud.HideTutorial(); return; }
            // Người chơi đang kéo: bỏ lớp tối và ngón tay, chỉ giữ lời nhắn.
            if (_sim.DraggingBlockId >= 0) { _hud.ShowTutorial(Message, null, null); return; }
            _focus.Clear();
            _view.CollectTutorialFocus(_focus);
            _hud.ShowTutorial(Message, _capture.Render(_focus), ScreenPath(block));
        }

        public void End()
        {
            if (Active) _view?.SetTutorialFocus(-1);
            _hud.HideTutorial();
            _capture.Release();
            _blockId = -1;
            _door = null;
            _path.Clear();
        }

        bool PickMove(LevelSolution solution)
        {
            if (solution != null && solution.HasSteps)
            {
                var step = solution.steps[0];
                if (_sim.Board.Find(step.block) != null)
                {
                    _blockId = step.block;
                    _path.AddRange(step.path);
                    _door = step.kind == SolutionStepKind.Exit ? step.door : (GateSide?)null;
                    return true;
                }
            }
            foreach (var b in _sim.Board.Blocks)
            {
                if (b.mystery || b.Frozen) continue;
                foreach (var g in _sim.Board.Gates)
                {
                    if (g.color != b.color || !DragSolver.Fits(b, g)) continue;
                    _blockId = b.id;
                    _door = g.side;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Đường kéo của ngón tay (pixel màn hình): tâm khối, các ô trên đường đi rồi ra cổng.</summary>
        List<Vector2> ScreenPath(BlockData b)
        {
            _screen.Clear();
            float x = b.gx, y = b.gy;
            _screen.Add(Project(x + b.w / 2f, y + b.h / 2f));
            foreach (var p in _path)
            {
                x = p.x; y = p.y;
                _screen.Add(Project(x + b.w / 2f, y + b.h / 2f));
            }
            if (_door.HasValue)
            {
                float cx = x + b.w / 2f, cy = y + b.h / 2f;
                switch (_door.Value)
                {
                    case GateSide.Top: cy = -ExitReach; break;
                    case GateSide.Bottom: cy = DragSolver.BoardHeight + ExitReach; break;
                    case GateSide.Left: cx = -ExitReach; break;
                    default: cx = DragSolver.BoardWidth + ExitReach; break;
                }
                // Không có đường đi định sẵn: ngón tay hướng thẳng vào tâm cổng.
                if (_path.Count == 0) SnapToGate(b, ref cx, ref cy);
                _screen.Add(Project(cx, cy));
            }
            return _screen;
        }

        void SnapToGate(BlockData b, ref float cx, ref float cy)
        {
            foreach (var g in _sim.Board.Gates)
            {
                if (g.side != _door.Value || g.color != b.color) continue;
                float mid = g.start + g.len / 2f;
                if (g.side == GateSide.Top || g.side == GateSide.Bottom) cx = mid; else cy = mid;
                return;
            }
        }

        Vector2 Project(float boardX, float boardY) =>
            _camera.WorldToScreenPoint(BoardSpace.BoardToWorld(boardX, boardY, 0, 0, BlockView.DragY));
    }
}
