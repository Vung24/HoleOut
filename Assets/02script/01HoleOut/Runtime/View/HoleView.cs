using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Hố trên khay chờ: thân khối thoát qua cổng thu dần thành hố, hố bay lên khay, mở/đóng cửa
    /// và cú nảy khi nhận nhân vật. Hiệu ứng hạt nằm ở <see cref="HoleEffects"/>.
    /// Pool hố giữ qua các màn; trạng thái hố đọc từ <see cref="Dock"/>.
    /// </summary>
    public sealed class HoleView
    {
        const float BlockHandoffStart = 0.6f;
        const float HoleTakeoffScale = 0.7f;
        // Hố đầy: phồng nhẹ khi cửa đóng, rồi co lại biến mất (theo Timer của trạng thái Closing).
        const float ClosePopDuration = 0.3f, ClosePopAmount = 0.12f, ShrinkStart = 0.48f, ShrinkDuration = 0.18f;
        // Booster nhấc: độ cao vòng bay của khối qua bàn.
        const float LiftArcHeight = 1.4f;

        sealed class BlockExit
        {
            public BoardBlockRig Rig;
            public Vector3 Start;
            public Vector3 End;
            public Vector3 HoleOffset;
            public Vector3 Scale;
            /// <summary>Khối được nhấc: bay từ vị trí cũ qua bàn tới Start trước khi thoát.</summary>
            public bool Lifted;
            public Vector3 LiftFrom;
        }

        readonly DockVisuals _visuals;
        readonly Transform _poolRoot;
        readonly Stack<HoleRig> _pool = new Stack<HoleRig>();
        readonly Dictionary<int, HoleRig> _holes = new Dictionary<int, HoleRig>();
        readonly Dictionary<int, BlockExit> _exits = new Dictionary<int, BlockExit>();
        readonly Dictionary<int, string> _holeColors = new Dictionary<int, string>();

        GameSimulation _sim;
        Transform _parent;
        bool _targeting;

        /// <summary>Tỉ lệ đưa prefab hố về đường kính 1 đơn vị.</summary>
        public float UnitScale { get; }
        /// <summary>Tỉ lệ hố của màn hiện tại.</summary>
        public float Scale { get; private set; } = 1;

        public HoleView(DockVisuals visuals, Transform poolRoot)
        {
            _visuals = visuals;
            _poolRoot = poolRoot;
            var hole = visuals.HolePrefab;
            float holeSize = 1;
            if (hole.baseRenderer is SkinnedMeshRenderer smr && smr.sharedMesh)
            {
                var rel = hole.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;
                holeSize = smr.sharedMesh.bounds.size.x * rel.lossyScale.x;
            }
            UnitScale = 1 / Mathf.Max(0.01f, holeSize);
        }

        public void Bind(GameSimulation sim, Transform parent, float scale)
        {
            _sim = sim;
            _parent = parent;
            Scale = scale;
        }

        public void Clear()
        {
            foreach (var h in _holes.Values) ReleaseRig(h);
            _holes.Clear();
            _holeColors.Clear();
            _exits.Clear();
            _sim = null;
            _parent = null;
        }

        public bool TryGet(int holeId, out HoleRig rig) => _holes.TryGetValue(holeId, out rig);

        /// <summary>Mã màu của hố đang hiển thị; null nếu không có.</summary>
        public string ColorOf(int holeId) => _holeColors.TryGetValue(holeId, out var code) ? code : null;

        /// <summary>Nam châm đang chờ chọn hố: hố trên khay phập phồng.</summary>
        public void SetTargeting(bool on) => _targeting = on;

        /// <summary>
        /// Khối rời bàn qua cổng <paramref name="side"/>: thân khối trượt ra ngoài mép cổng trong lúc thu vào quanh miệng hố.
        /// <paramref name="extension"/> là phần thân đã vượt qua mép (đơn vị ô).
        /// </summary>
        public void AddExit(BlockData block, BoardBlockRig rig, GateSide side, Vector3 start, float extension, bool lifted)
        {
            var direction = side switch
            {
                GateSide.Top => Vector3.forward,
                GateSide.Bottom => Vector3.back,
                GateSide.Left => Vector3.left,
                _ => Vector3.right
            };
            int length = BoardRig.IsHorizontal(side) ? block.h : block.w;
            // Đưa tâm khối ra ngoài mép cổng trong khi thân thu vào quanh miệng hố.
            float distance = (length * 0.5f - extension) * BoardSpace.BoardCell + BoardRig.Margin;
            var end = start + direction * distance;
            var offset = Vector3.up * 0.3f;
            var scale = Vector3.one;
            var from = start;
            if (rig)
            {
                rig.SetSelected(true);
                from = rig.transform.position;
                rig.transform.position = start;
                offset = rig.HoleWorldPosition - start;
                rig.transform.position = lifted ? from : start;
                scale = rig.transform.localScale;
            }
            _exits[block.id] = new BlockExit
            {
                Rig = rig, Start = start, End = end, HoleOffset = offset, Scale = scale, Lifted = lifted, LiftFrom = from
            };
        }

        // ───────────── Sự kiện ─────────────

        public void OnLaunched(int id)
        {
            var hole = Find(_sim.Dock.Flying, id) ?? Find(_sim.Dock.Docked, id);
            if (hole == null) return;
            var rig = _pool.Count > 0 ? _pool.Pop() : Object.Instantiate(_visuals.HolePrefab, _poolRoot);
            rig.transform.SetParent(_parent, false);
            rig.Spawn(_visuals.Palette.Get(hole.Color), _visuals.Palette.CharacterMaterial(hole.Color));
            rig.SetCount(hole.RemainingCapacity);
            _holes[id] = rig;
            _holeColors[id] = hole.Color;
        }

        public void OnDocked(int id)
        {
            if (!_holes.TryGetValue(id, out var rig)) return;
            rig.Open();
            // Đáp xuống hốc: nảy cùng nhịp với cú rung HoleDocked.
            rig.Punch();
        }

        public void OnClosing(int id) { if (_holes.TryGetValue(id, out var rig)) rig.Close(); }

        public void OnReleased(int id)
        {
            if (!_holes.TryGetValue(id, out var rig)) return;
            ReleaseRig(rig);
            _holes.Remove(id);
            _holeColors.Remove(id);
        }

        // ───────────── Vẽ mỗi frame ─────────────

        public void Render()
        {
            int slots = _sim.Dock.SlotCount;
            foreach (var h in _sim.Dock.Flying) Place(h, slots);
            foreach (var h in _sim.Dock.Docked) Place(h, slots);
        }

        void Place(Hole h, int slots)
        {
            if (!_holes.TryGetValue(h.Id, out var rig)) return;
            var slot = BoardSpace.ToWorld(_sim.Layout.DockSlot(h.SlotIndex, slots), 0.02f);
            _exits.TryGetValue(h.Id, out var exit);
            float scale = 1;
            Vector3 pos = slot;
            if (h.State == HoleState.Flying)
            {
                if (exit != null && exit.Lifted && h.Timer < Dock.FlightStart)
                {
                    // Nhấc: khối bay vòng qua bàn tới sát cổng, hố chưa hiện.
                    float k = BoardSpace.Smooth(Mathf.InverseLerp((float)h.LaunchStart, (float)Dock.FlightStart, (float)h.Timer));
                    var body = Vector3.Lerp(exit.LiftFrom, exit.Start, k) + Vector3.up * (LiftArcHeight * 4 * k * (1 - k));
                    if (exit.Rig) exit.Rig.transform.position = body;
                    pos = body + exit.HoleOffset;
                    scale = 0;
                }
                else if (h.Timer < 0)
                {
                    float slide = Mathf.InverseLerp((float)Dock.FlightStart, 0, (float)h.Timer);
                    float handoff = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BlockHandoffStart, 1, slide));
                    if (exit != null)
                    {
                        float travel = 1 - (1 - slide) * (1 - slide);
                        pos = Vector3.Lerp(exit.Start, exit.End, travel) + exit.HoleOffset;
                        if (exit.Rig)
                        {
                            float bodyScale = 1 - handoff;
                            exit.Rig.transform.localScale = exit.Scale * bodyScale;
                            exit.Rig.transform.position = pos - exit.HoleOffset * bodyScale;
                        }
                    }
                    scale = HoleTakeoffScale * handoff;
                }
                else
                {
                    FinishExit(exit);
                    float t = Mathf.Clamp01((float)h.Timer);
                    float travel = Mathf.SmoothStep(0, 1, t);
                    var from = exit != null ? exit.End + exit.HoleOffset : slot;
                    float arc = 4 * t * (1 - t);
                    pos = Vector3.Lerp(from, slot, travel) + Vector3.up * (_visuals.FlightHeight * arc * arc);
                    scale = Mathf.Lerp(HoleTakeoffScale, 1, travel);
                }
            }
            else
            {
                FinishExit(exit);
                _exits.Remove(h.Id);
                if (h.State == HoleState.Closing)
                {
                    float t = (float)h.Timer;
                    float pop = 1 + ClosePopAmount * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / ClosePopDuration));
                    scale = pop * (1 - Mathf.Clamp01((t - ShrinkStart) / ShrinkDuration));
                }
            }
            if (_targeting && h.State == HoleState.Docked) scale *= 1 + 0.07f * Mathf.Sin(Time.time * 8 + h.SlotIndex);
            rig.transform.position = pos;
            rig.SetCount(h.RemainingCapacity);
            rig.transform.localScale = Vector3.one * Scale * scale * rig.CatchScale(_visuals.CatchPunch);
        }

        /// <summary>Id hố đã đáp lên khay nằm dưới tia chạm; -1 nếu không trúng.</summary>
        public int HoleAt(Ray ray)
        {
            if (_sim == null) return -1;
            var dock = _sim.Dock;
            int count = dock.SlotCount;
            float radius = count > 1 ? (float)Vec2.Distance(_sim.Layout.DockSlot(0, count), _sim.Layout.DockSlot(1, count)) * 0.5f : 0.8f;
            int best = -1;
            float bestDistance = radius;
            foreach (var h in dock.Docked)
            {
                if (h.State != HoleState.Docked || !_holes.TryGetValue(h.Id, out var rig)) continue;
                var center = rig.transform.position;
                if (!new Plane(Vector3.up, center).Raycast(ray, out float enter)) continue;
                var hit = ray.GetPoint(enter);
                float d = Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(center.x, center.z));
                if (d < bestDistance) { bestDistance = d; best = h.Id; }
            }
            return best;
        }

        // ───────────── Tiện ích ─────────────

        static void FinishExit(BlockExit exit)
        {
            if (exit == null || !exit.Rig) return;
            exit.Rig.gameObject.SetActive(false);
            Object.Destroy(exit.Rig.gameObject);
            exit.Rig = null;
        }

        void ReleaseRig(HoleRig rig)
        {
            if (!rig) return;
            rig.Despawn();
            rig.transform.SetParent(_poolRoot, false);
            _pool.Push(rig);
        }


        static Hole Find(List<Hole> list, int id)
        {
            foreach (var h in list) if (h.Id == id) return h;
            return null;
        }
    }
}
