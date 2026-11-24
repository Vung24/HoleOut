using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Khối trên bàn của một màn: dựng rig theo kích thước/hình, bám vị trí kéo của mô phỏng
    /// và đổi màu theo trạng thái (bí ẩn, đang chọn). Khối rời bàn được giao cho <see cref="HoleView"/>.
    /// </summary>
    public sealed class BlockView
    {
        // Gốc thân nằm sát mặt bàn, nhấc nhẹ khi kéo.
        public const float RestY = 0.06f;
        public const float DragY = 0.2f;

        readonly GameSimulation _sim;
        readonly BoardVisuals _visuals;
        readonly BoardRig _board;
        readonly Dictionary<int, BoardBlockRig> _blocks = new Dictionary<int, BoardBlockRig>();
        readonly HashSet<int> _alive = new HashSet<int>();
        readonly List<int> _dead = new List<int>();
        // Tiến độ nâng/hạ của từng khối (0..1) và chiều hiện tại.
        readonly Dictionary<int, LiftState> _lift = new Dictionary<int, LiftState>();
        bool _targeting;
        int _shakeId = -1;
        float _shakeStart;

        struct LiftState { public float t; public bool up; }

        public BlockView(GameSimulation sim, BoardVisuals visuals, BoardRig board)
        {
            _sim = sim;
            _visuals = visuals;
            _board = board;
        }

        /// <summary>Booster nhấc đang chờ chọn khối: khối nhấc được nhún nhẹ.</summary>
        public void SetTargeting(bool on) => _targeting = on;

        int _focusId = -1;

        /// <summary>Khối được hướng dẫn chỉ vào: nhún và phồng nhịp, cổng cùng màu sáng theo; -1 để tắt.</summary>
        public void SetFocus(int blockId) => _focusId = blockId;

        readonly List<Renderer> _rigRenderers = new List<Renderer>();

        /// <summary>Renderer của khối đang được chỉ và các cổng cùng màu (để làm sáng riêng trên lớp tối).</summary>
        public void CollectFocus(List<Renderer> into)
        {
            var block = _sim.Board.Find(_focusId);
            if (block == null || !_blocks.TryGetValue(_focusId, out var rig) || !rig) return;
            rig.GetComponentsInChildren(false, _rigRenderers);
            into.AddRange(_rigRenderers);
            _board.CollectGateRenderers(block.color, into);
        }

        /// <summary>Khối chạm cổng khi khay đầy: lắc ngang ngắn như lắc đầu.</summary>
        public void PlayDockFull(int blockId)
        {
            _shakeId = blockId;
            _shakeStart = Time.time;
        }

        Vector3 ShakeOffset(int blockId)
        {
            float d = _visuals.DockFullAlertDuration;
            if (blockId != _shakeId || d <= 0) return Vector3.zero;
            float k = (Time.time - _shakeStart) / d;
            if (k >= 1) { _shakeId = -1; return Vector3.zero; }
            return Vector3.right * Mathf.Sin(k * Mathf.PI * 6) * (1 - k) * _visuals.DockFullShake;
        }

        /// <summary>Gỡ rig của khối vừa rời bàn để chuyển sang hiệu ứng thoát (có thể null nếu thiếu prefab).</summary>
        public BoardBlockRig Take(int blockId)
        {
            _blocks.TryGetValue(blockId, out var rig);
            _blocks.Remove(blockId);
            return rig;
        }

        public void Render()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _alive.Clear();
            string draggedColor = null, focusColor = null;
            float focusPulse = Mathf.Abs(Mathf.Sin(Time.time * 4));
            foreach (var b in _sim.Board.Blocks)
            {
                _alive.Add(b.id);
                if (!_blocks.TryGetValue(b.id, out var rig)) _blocks[b.id] = rig = Create(b);
                if (!rig) continue;
                bool dragging = _sim.DraggingBlockId == b.id;
                rig.SetSelected(dragging);
                if (dragging && !b.mystery) draggedColor = b.color;
                float lift = UpdateLift(b.id, dragging, dt);
                bool focused = b.id == _focusId && !dragging;
                if (focused) focusColor = b.color;
                rig.SetLiftScale(focused ? lift + focusPulse * 1.5f : lift);
                bool moved = dragging && _sim.HasDragResult;
                double vx = moved ? _sim.LastDrag.VisualX : b.gx, vy = moved ? _sim.LastDrag.VisualY : b.gy;
                var target = BoardSpace.BoardToWorld(vx, vy, b.w, b.h, Mathf.Lerp(RestY, DragY, Mathf.Clamp01(lift)));
                if (_targeting && !b.mystery && !b.Frozen) target += BoardSpace.LeanRotation * Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 6 + b.id)) * 0.12f;
                else if (focused) target += BoardSpace.LeanRotation * Vector3.up * focusPulse * 0.12f;
                target += ShakeOffset(b.id);
                if (dragging) rig.transform.position = target;
                else
                {
                    // Trượt về ô sau khi thả: giữ độ cao theo nhịp hạ, mặt phẳng bám dần không phụ thuộc FPS.
                    var p = rig.transform.position;
                    float k = 1 - Mathf.Exp(-_visuals.BlockSettleSharpness * dt);
                    rig.transform.position = Vector3.Lerp(p, target, k);
                }
                rig.SetLook(_visuals.Palette.Get(b.mystery ? "M" : b.color), b.mystery, b.capacity, b.ice);
                rig.SetIce(b.ice, _visuals.Ice);
            }
            // Gợi ý đích: cổng cùng màu khối đang kéo (hoặc khối hướng dẫn) sáng nhịp nhẹ.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f);
            if (draggedColor != null) _board.SetGateHighlight(draggedColor, _visuals.GateDragHighlight * pulse);
            else _board.SetGateHighlight(focusColor, _visuals.GateDragHighlight * focusPulse);
            _dead.Clear();
            foreach (var id in _blocks.Keys) if (!_alive.Contains(id)) _dead.Add(id);
            foreach (var id in _dead) { if (_blocks[id]) Object.Destroy(_blocks[id].gameObject); _blocks.Remove(id); }
            foreach (var id in _dead) _lift.Remove(id);
        }

        /// <summary>Nhịp nâng/hạ: trả về 0 (nằm) → 1 (nhấc), có nảy nhẹ ở cuối mỗi chiều.</summary>
        float UpdateLift(int id, bool up, float dt)
        {
            // Khối mới bắt đầu ở trạng thái nằm yên.
            if (!_lift.TryGetValue(id, out var s)) s = new LiftState { t = 1, up = false };
            // Đổi chiều giữa chừng: giữ nguyên độ cao hiện tại.
            if (s.up != up) { s.up = up; s.t = 1 - s.t; }
            float duration = _visuals.BlockLiftDuration;
            s.t = duration > 0 ? Mathf.Min(1, s.t + dt / duration) : 1;
            _lift[id] = s;
            float eased = OutBack(s.t, _visuals.BlockLiftBounce);
            return up ? eased : 1 - eased;
        }

        static float OutBack(float x, float c)
        {
            float u = x - 1;
            return 1 + (c + 1) * u * u * u + c * u * u;
        }

        BoardBlockRig Create(BlockData b)
        {
            var prefab = _visuals.BoardBlock(b.w, b.h, b.shape);
            if (!prefab)
            {
                Debug.LogError($"[HoleOut] Thiếu prefab khối {b.w}x{b.h} ({b.shape}) trong cấu hình prefab của HoleOutVisuals.");
                return null;
            }
            var rig = Object.Instantiate(prefab, _board.blocksRoot);
            rig.name = "Block" + b.id;
            // Prefab dựng theo 1 đơn vị mỗi ô.
            rig.transform.localScale = Vector3.one * BoardSpace.BoardCell;
            rig.transform.position = BoardSpace.BoardToWorld(b.gx, b.gy, b.w, b.h, RestY);
            return rig;
        }
    }
}
