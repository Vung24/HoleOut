using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Luồng dùng booster trong màn: mở khóa theo màn, trừ lượt, chờ chạm mục tiêu (Lift: khối, Magnet: hố)
    /// và cập nhật nút/gợi ý trên HUD. Hiệu ứng lên bàn chơi do <see cref="GameSimulation"/> quyết định.
    /// </summary>
    public sealed class BoosterController
    {
        readonly List<BoosterSettings> _settings;
        readonly GameHud _hud;
        readonly BoardInput _input;
        readonly Camera _camera;

        GameSimulation _sim;
        GameView _view;
        int _levelNumber;
        // Booster đang chờ người chơi chạm mục tiêu.
        BoosterType? _pending;

        public BoosterController(List<BoosterSettings> settings, GameHud hud, BoardInput input, Camera camera)
        {
            _settings = settings;
            _hud = hud;
            _input = input;
            _camera = camera;
        }

        public bool HasPending => _pending.HasValue;

        /// <summary>Gợi ý hiện khi không có booster nào chờ mục tiêu (ví dụ hướng dẫn màn đầu); null để ẩn.</summary>
        public string IdleHint { get; set; }

        /// <summary>Giá mua thêm một lượt bằng coin (0 = không bán).</summary>
        public System.Func<BoosterType, int> Price { get; set; }

        string _notice;
        float _noticeUntil;
        int PriceOf(BoosterType type) => Price != null ? Price(type) : 0;

        public void Bind(GameSimulation sim, GameView view, int levelNumber)
        {
            _sim = sim;
            _view = view;
            _levelNumber = levelNumber;
            _pending = null;
            _notice = null;
            _noticeUntil = 0;
        }

        public void CancelPending() => _pending = null;

        /// <summary>Màn mở khóa booster: lần đầu tiên hiện bảng giới thiệu (mỗi booster một lần).</summary>
        public void AnnounceUnlock()
        {
            foreach (var s in _settings)
            {
                if (s.unlockLevel != _levelNumber || BoosterInventory.Announced(s.type)) continue;
                BoosterInventory.MarkAnnounced(s.type);
                _hud.ShowBoosterUnlocked(s.type);
                return;
            }
        }

        /// <summary>Bàn chơi nhận thao tác: đang chơi, đã hết cảnh mở màn, không có bảng che.</summary>
        public bool BoardInputOpen => _sim != null && _sim.Status == GameStatus.Playing && !_view.IntroPlaying && !_hud.BlocksBoardInput;

        BoosterSettings Settings(BoosterType type)
        {
            foreach (var b in _settings) if (b.type == type) return b;
            return null;
        }

        bool IsUnlocked(BoosterSettings s) => s != null && _levelNumber >= s.unlockLevel;

        /// <summary>Điều kiện riêng của từng booster ngoài số lượt (khay còn chỗ, còn thêm được khay...).</summary>
        bool CanUse(BoosterType type)
        {
            if (!BoardInputOpen || !_sim.CanUseBooster) return false;
            switch (type)
            {
                case BoosterType.Lift: return !_sim.Dock.IsFull && _sim.Board.Blocks.Count > 0;
                case BoosterType.ExtraSlot: return _sim.Dock.CanAddSlot;
                default: return _sim.Dock.Docked.Count > 0;
            }
        }

        /// <summary>Bấm nút booster: bấm lại booster đang chờ thì hủy chọn.</summary>
        public void Press(BoosterType type)
        {
            var s = Settings(type);
            if (_pending == type) { _pending = null; return; }
            if (!IsUnlocked(s) || !CanUse(type)) return;
            if (BoosterInventory.Count(s) <= 0)
            {
                int price = PriceOf(type);
                if (price <= 0) return;
                var simulation = _sim;
                _pending = null;
                _hud.ShowBoosterPurchase(type, price, CoinWallet.Shared.Balance >= price, () =>
                {
                    if (_sim != simulation || !CanUse(type)) return;
                    if (BoosterInventory.Count(s) <= 0)
                    {
                        if (!CoinWallet.Shared.TrySpend(price))
                        {
                            _notice = $"Cần {price} coin";
                            _noticeUntil = Time.unscaledTime + 2f;
                            return;
                        }
                        BoosterInventory.Set(type, 1);
                    }
                    Activate(s);
                });
                return;
            }
            Activate(s);
        }

        void Activate(BoosterSettings s)
        {
            _input.Cancel(_sim);

            if (s.type == BoosterType.ExtraSlot)
            {
                if (_sim.TryAddSlot()) BoosterInventory.TryConsume(s);
                _pending = null;
                return;
            }
            _pending = s.type;
        }

        /// <summary>Chạm khi booster chờ mục tiêu. Chạm trượt mục tiêu giữ nguyên lượt chọn.</summary>
        public void HandleTarget(Vector2 screen)
        {
            var type = _pending.Value;
            var s = Settings(type);
            if (type == BoosterType.Lift)
            {
                if (!_input.ScreenToBoard(screen, out var cell)) return;
                var b = BoardInput.BlockAt(_sim, cell);
                if (b != null && _sim.TryLiftBlock(b.id, out var side))
                {
                    _view.NoteLift(b, side);
                    BoosterInventory.TryConsume(s);
                    _pending = null;
                }
                return;
            }

            int holeId = _view.HoleAt(_camera.ScreenPointToRay(screen));
            if (holeId >= 0 && _sim.TryMagnet(holeId) > 0)
            {
                BoosterInventory.TryConsume(s);
                _pending = null;
            }
        }

        /// <summary>Đồng bộ nút, gợi ý và đánh dấu mục tiêu theo trạng thái hiện tại.</summary>
        public void Refresh()
        {
            // Mất điều kiện (kết thúc màn, khay đầy...) thì hủy lượt chọn mục tiêu, không trừ lượt.
            if (_pending.HasValue && !CanUse(_pending.Value)) _pending = null;
            foreach (var s in _settings)
            {
                bool unlocked = IsUnlocked(s);
                _hud.SetBooster(s.type, !unlocked, s.unlockLevel, BoosterInventory.Count(s),
                    unlocked && CanUse(s.type), _pending == s.type, PriceOf(s.type) > 0);
            }
            _hud.SetBoosterHint(_pending == BoosterType.Lift ? "Chạm một khối để nhấc ra cổng"
                : _pending == BoosterType.Magnet ? "Chạm một hố trên khay để hút cùng màu"
                : Time.unscaledTime < _noticeUntil ? _notice : IdleHint);
            _view.SetBoosterTargeting(_pending == BoosterType.Lift, _pending == BoosterType.Magnet);
        }
    }
}
