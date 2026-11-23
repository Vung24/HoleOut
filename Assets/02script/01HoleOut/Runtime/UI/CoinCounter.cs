using TMPro;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Bộ đếm coin trên Home/HUD: hiện số dư của <see cref="CoinWallet"/>, đếm dần khi số dư đổi.</summary>
    public class CoinCounter : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [Tooltip("Biểu tượng coin; là đích bay của coin khi nhận thưởng.")]
        [SerializeField] RectTransform icon;
        [Min(0.05f)] [SerializeField] float countTime = 0.5f;

        int _shown, _target, _from;
        float _countStart = -1;

        public RectTransform Icon => icon ? icon : (RectTransform)transform;

        void OnEnable()
        {
            CoinWallet.Changed += OnChanged;
            _shown = _target = CoinWallet.Shared.Balance;
            _countStart = -1;
            Show(_shown);
        }

        void OnDisable() => CoinWallet.Changed -= OnChanged;

        void OnChanged(int balance)
        {
            _from = _shown;
            _target = balance;
            _countStart = Time.unscaledTime;
        }

        void Update()
        {
            if (_countStart < 0) return;
            float k = Mathf.Clamp01((Time.unscaledTime - _countStart) / countTime);
            int value = Mathf.RoundToInt(Mathf.Lerp(_from, _target, 1 - (1 - k) * (1 - k)));
            if (value != _shown) Show(value);
            if (k >= 1) _countStart = -1;
        }

        void Show(int value)
        {
            _shown = value;
            if (label) label.text = value.ToString();
        }
    }
}
