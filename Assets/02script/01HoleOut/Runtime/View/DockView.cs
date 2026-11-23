using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Khay chờ: khay bo góc từ prefab và một hốc (prefab ô chờ) cho mỗi vị trí đặt hố.</summary>
    public sealed class DockView
    {
        const float Depth = 1.7f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        readonly float _socketFill;
        readonly DockVisuals _visuals;
        readonly Renderer[] _socketRenderers = new Renderer[Dock.MaxSlotsLimit];
        readonly MaterialPropertyBlock _warnProps = new MaterialPropertyBlock();
        Color _socketColor;
        int _warnSlot = -1;
        readonly Renderer _rim;
        readonly MaterialPropertyBlock _rimProps = new MaterialPropertyBlock();
        Color _rimColor;
        float _fullStart = -999f;
        bool _rimTinted;

        readonly Transform _root;
        readonly Transform[] _slots = new Transform[Dock.MaxSlotsLimit];
        readonly float _slotUnit;
        int _shownCount = -1;

        public DockView(Transform parent, float width, DockVisuals visuals)
        {
            _socketFill = visuals.SocketFill;
            _visuals = visuals;
            _root = new GameObject("WaitingDock").transform;
            _root.SetParent(parent, false);
            float z = -(float)DefaultBoardLayout.DockZ;

            var tray = Object.Instantiate(visuals.TrayPrefab, _root);
            tray.name = "Tray";
            tray.SetBounds(-width / 2, width / 2, z - Depth / 2, z + Depth / 2);
            _rim = tray.rimLayer ? tray.rimLayer.GetComponent<MeshRenderer>() : null;
            var rimMaterial = _rim ? _rim.sharedMaterial : null;
            if (rimMaterial && rimMaterial.HasProperty(BaseColorId)) _rimColor = rimMaterial.GetColor(BaseColorId);
            else _rim = null;

            var probe = visuals.SlotPrefab.GetComponentInChildren<MeshRenderer>(true);
            _slotUnit = probe ? Mathf.Max(0.01f, probe.bounds.size.x) : 1;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = Object.Instantiate(visuals.SlotPrefab, _root).transform;
                slot.name = "Socket" + (i + 1);
                _slots[i] = slot;
                // Hốc là mesh của prefab ô chờ (renderer đầu tiên); bóng tròn bên dưới không đổi màu.
                _socketRenderers[i] = slot.GetComponentInChildren<MeshRenderer>(true);
            }
            var socketMaterial = _socketRenderers[0] ? _socketRenderers[0].sharedMaterial : null;
            _socketColor = socketMaterial && socketMaterial.HasProperty(BaseColorId) ? socketMaterial.GetColor(BaseColorId) : Color.black;
        }

        /// <summary>
        /// Còn đúng một chỗ trống (và không hố nào sắp đóng trả chỗ): hốc trống đó ửng màu cảnh báo theo nhịp chậm.
        /// </summary>
        public void RenderWarning(GameSimulation sim, float time)
        {
            var dock = sim.Dock;
            int slot = -1;
            if (_visuals.WarnStrength > 0 && sim.Status == GameStatus.Playing
                && dock.SlotCount - dock.Occupied == 1 && !dock.AnyClosing())
                slot = dock.FirstFreeSlot();
            if (slot != _warnSlot && _warnSlot >= 0) SetSocketColor(_warnSlot, _socketColor);
            _warnSlot = slot;
            if (slot < 0) return;
            float pulse = 0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2 / 1.2f);
            SetSocketColor(slot, Color.Lerp(_socketColor, _visuals.WarnColor, pulse * _visuals.WarnStrength));
        }

        /// <summary>Khối bị chặn vì khay đầy: viền khay chớp màu cảnh báo hai nhịp.</summary>
        public void PlayFull(float time) => _fullStart = time;

        public void RenderFullAlert(float time)
        {
            if (!_rim) return;
            float d = _visuals.FullAlertDuration;
            float k = d > 0 ? (time - _fullStart) / d : 1;
            if (k >= 1)
            {
                if (_rimTinted) { _rim.SetPropertyBlock(null); _rimTinted = false; }
                return;
            }
            float flash = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2)) * (1 - 0.4f * k);
            _rim.GetPropertyBlock(_rimProps);
            _rimProps.SetColor(BaseColorId, Color.Lerp(_rimColor, _visuals.WarnColor, flash));
            _rim.SetPropertyBlock(_rimProps);
            _rimTinted = true;
        }

        void SetSocketColor(int slot, Color color)
        {
            var r = _socketRenderers[slot];
            if (!r) return;
            r.GetPropertyBlock(_warnProps);
            _warnProps.SetColor(BaseColorId, color);
            r.SetPropertyBlock(_warnProps);
        }

        /// <summary>Đặt lại hốc khi số vị trí thay đổi; không làm gì nếu giữ nguyên.</summary>
        public void Render(GameSimulation sim)
        {
            int count = sim.Dock.SlotCount;
            if (count == _shownCount) return;
            _shownCount = count;
            float spacing = count > 1 ? (float)Vec2.Distance(sim.Layout.DockSlot(0, count), sim.Layout.DockSlot(1, count)) : 1.2f;
            float scale = spacing * _socketFill / _slotUnit;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                slot.gameObject.SetActive(i < count);
                if (i >= count) continue;
                slot.position = BoardSpace.ToWorld(sim.Layout.DockSlot(i, count), 0.01f);
                slot.localScale = Vector3.one * scale;
            }
        }

        public void Clear()
        {
            if (_root) Object.Destroy(_root.gameObject);
        }
    }
}
