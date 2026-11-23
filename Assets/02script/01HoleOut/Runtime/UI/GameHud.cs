using System;
using HoleOut.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    public class GameHud : MonoBehaviour
    {
        public Action OnPrev, OnRetry, OnNext, OnContinue, OnHome;
        public Action<BoosterType> OnBooster;
        /// <summary>Nhận thưởng thắng; tham số true là nhân đôi.</summary>
        public Action<bool> OnClaim;
        public Action OnButtonClick;

        [SerializeField] TMP_Text _level;
        [SerializeField] Image _levelPill;
        [Tooltip("Nền ô tên màn khi màn có nhiều đợt khối.")]
        [SerializeField] Sprite _multiWavePill;
        Sprite _defaultPill;
        [SerializeField] RectTransform _topBarBottom, _bottomBar, _safe;
        [SerializeField] Image _pictureProgressFill;
        [SerializeField] TMP_Text _pictureProgressText;
        [SerializeField] SettingsView _settingsView = new();
        [SerializeField] BoosterBar _boosterBar = new();
        [SerializeField] GameplayPopup _gameplayPopup = new();

        static readonly Vector3[] Corners = new Vector3[4];
        int _shownPercent = -1;

        public bool PanelVisible => _gameplayPopup.Visible;
        public bool BlocksBoardInput => PanelVisible || _settingsView.Visible;
        public TMP_FontAsset LevelFont => _level.font;

        public void Init()
        {
            _settingsView.Bind(Bind, () => OnPrev?.Invoke(), () => OnNext?.Invoke(), Retry, () => OnHome?.Invoke());
            _boosterBar.Bind(Bind, type => OnBooster?.Invoke(type));
            _gameplayPopup.Bind(Bind);
            ApplySafeArea();
        }

        public void SetLevel(int levelNumber)
        {
            string label = "Màn " + levelNumber;
            if (_level.text != label) _level.text = label;
            _settingsView.SetLevel(label);
        }

        public void SetPictureProgress(int completed, int total)
        {
            float fraction = total > 0 ? Mathf.Clamp01((float)completed / total) : 0f;
            int percent = total > 0 ? (int)(100L * Mathf.Clamp(completed, 0, total) / total) : 0;
            if (_pictureProgressFill && _pictureProgressFill.fillAmount != fraction)
                _pictureProgressFill.fillAmount = fraction;
            if (_shownPercent == percent) return;
            _shownPercent = percent;
            if (_pictureProgressText) _pictureProgressText.SetText("{0}%", percent);
        }

        /// <summary>Đổi nền ô tên màn theo số đợt; báo popup khi vào màn nhiều đợt nếu chưa có popup khác.</summary>
        public void SetWaves(int waveCount, bool announce)
        {
            bool multi = waveCount > 1;
            if (_levelPill)
            {
                if (!_defaultPill) _defaultPill = _levelPill.sprite;
                _levelPill.sprite = multi && _multiWavePill ? _multiWavePill : _defaultPill;
            }
            if (multi && announce && !PanelVisible)
            {
                _settingsView.Hide();
                _gameplayPopup.ShowWaves(waveCount);
            }
        }

        public void SetBooster(BoosterType type, bool locked, int unlockLevel, int count, bool usable, bool selected, bool buyable = false)
        {
            _boosterBar.SetBooster(type, locked, unlockLevel, count, usable, selected, buyable);
        }

        public void SetBoosterHint(string text) => _boosterBar.SetHint(text);

        public void ShowWon(int coins = 0, int doubledCoins = 0, string body = null)
        {
            _settingsView.Hide();
            _gameplayPopup.ShowWon(coins, doubledCoins, body,
                doubled => OnClaim?.Invoke(doubled), () => OnNext?.Invoke(), () => OnRetry?.Invoke());
        }

        public void ShowBoosterUnlocked(BoosterType type)
        {
            _settingsView.Hide();
            _gameplayPopup.ShowBoosterUnlocked(type);
        }

        public void ShowBoosterPurchase(BoosterType type, int price, bool affordable, Action onPurchase)
        {
            _settingsView.Hide();
            _gameplayPopup.ShowBoosterPurchase(type, price, affordable, onPurchase);
        }

        public void ShowJammed(bool canContinue)
        {
            _settingsView.Hide();
            _gameplayPopup.ShowJammed(canContinue, () => OnContinue?.Invoke(), () => OnRetry?.Invoke());
        }

        public void HidePanel() => _gameplayPopup.Hide();

        /// <summary>Phần chiều cao màn hình (tính từ đỉnh) mà thanh HUD trên đang chiếm.</summary>
        public float TopBarFraction()
        {
            if (_topBarBottom == null || Screen.height <= 0) return 0.18f;
            Canvas.ForceUpdateCanvases();
            _topBarBottom.GetWorldCorners(Corners); // Overlay: tọa độ màn hình.
            float bottomY = Mathf.Min(Corners[0].y, Corners[3].y);
            return Mathf.Clamp01(1 - bottomY / Screen.height);
        }

        public float BottomBarFraction()
        {
            if (_bottomBar == null || Screen.height <= 0) return 0.14f;
            Canvas.ForceUpdateCanvases();
            _bottomBar.GetWorldCorners(Corners);
            return Mathf.Clamp01(Corners[1].y / Screen.height);
        }

        void Retry()
        {
            _settingsView.Hide();
            OnRetry?.Invoke();
        }

        void Bind(Button button, UnityAction action)
        {
            if (!button) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OnButtonClick?.Invoke());
            button.onClick.AddListener(action);
        }

        void Update() => ApplySafeArea();

        void ApplySafeArea()
        {
            SafeAreaLayout.Apply(_safe);
        }
    }

    internal static class SafeAreaLayout
    {
        public static void Apply(RectTransform rect)
        {
            if (!rect || Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            var min = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            var max = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            if (rect.anchorMin == min && rect.anchorMax == max) return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
