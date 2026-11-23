using System;
using HoleOut.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    [Serializable]
    internal sealed class GameplayPopup
    {
        [SerializeField] GameObject _panel;
        [SerializeField] TMP_Text _panelTitle, _panelBody;
        [SerializeField] Button _primary, _secondary;
        [SerializeField] TMP_Text _primaryText, _secondaryText;
        [SerializeField] Image _primaryImage;
        [SerializeField] Color _primaryColor = new Color32(119, 88, 204, 255);
        [SerializeField] Color _disabledPrimaryColor = new Color32(170, 170, 190, 255);

        Action _onPrimary, _onSecondary;
        RectTransform _primaryRect;
        Vector2 _primaryBasePos;

        public bool Visible => _panel != null && _panel.activeSelf;

        public void Bind(Action<Button, UnityAction> bind)
        {
            bind(_primary, () => _onPrimary?.Invoke());
            bind(_secondary, () => _onSecondary?.Invoke());
        }

        public void ShowWon(int coins, int doubledCoins, string body,
            Action<bool> onClaim, Action onNext, Action onRetry)
        {
            const string defaultBody = "Cả đám đông đã về hố!";
            if (coins > 0)
                Show("Hoàn thành!", string.IsNullOrEmpty(body) ? defaultBody : body,
                    $"x2  +{doubledCoins}", true, () => onClaim(true), $"Nhận +{coins}", () => onClaim(false));
            else
                Show("Hoàn thành!", defaultBody, "Màn tiếp", true, onNext, "Chơi lại", onRetry);
        }

        public void ShowBoosterUnlocked(BoosterType type)
        {
            string body = type switch
            {
                BoosterType.Lift => "Nhấc khối: chạm một khối để đưa thẳng ra cổng cùng màu.",
                BoosterType.ExtraSlot => "Thêm khay: mở thêm một chỗ trên khay chờ.",
                _ => "Nam châm: chạm một hố trên khay để hút ngay nhân vật cùng màu."
            };
            Show("Booster mới!", body, "Đã hiểu", true, Hide, null, null);
        }

        public void ShowWaves(int waveCount)
        {
            Show("Nhiều đợt!", $"Màn này có {waveCount} đợt khối.\nDọn sạch bàn để đợt tiếp theo xuất hiện.",
                "Chơi", true, Hide, null, null);
        }

        public void ShowJammed(bool canContinue, Action onContinue, Action onRetry)
        {
            Show("Hết chỗ!", canContinue ? "Khay chờ đã đầy.\nThêm 1 khay để chơi tiếp?" : "Khay chờ đã đầy và không thể thêm khay.",
                canContinue ? "Thêm khay" : "Đã tối đa", canContinue, onContinue, "Chơi lại", onRetry);
        }

        public void Hide()
        {
            if (Visible) _panel.SetActive(false);
        }

        void Show(string title, string body, string primary, bool primaryEnabled, Action onPrimary, string secondary, Action onSecondary)
        {
            _panelTitle.text = title;
            _panelBody.text = body;
            _primaryText.text = primary;
            _primaryImage.color = primaryEnabled ? _primaryColor : _disabledPrimaryColor;
            _primary.interactable = primaryEnabled;

            if (!_primaryRect)
            {
                _primaryRect = (RectTransform)_primary.transform;
                _primaryBasePos = _primaryRect.anchoredPosition;
            }

            bool hasSecondary = secondary != null;
            _secondary.gameObject.SetActive(hasSecondary);
            _primaryRect.anchoredPosition = hasSecondary ? _primaryBasePos : new Vector2(0, _primaryBasePos.y);
            if (hasSecondary) _secondaryText.text = secondary;
            _onPrimary = onPrimary;
            _onSecondary = onSecondary;
            _panel.SetActive(true);
        }
    }
}
