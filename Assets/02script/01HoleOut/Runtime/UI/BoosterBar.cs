using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    [Serializable]
    internal sealed class BoosterBar
    {
        [Tooltip("Nút booster theo thứ tự HoleOutBooster: Lift, ExtraSlot, Magnet.")]
        [SerializeField] BoosterButton[] _boosters = Array.Empty<BoosterButton>();
        [SerializeField] GameObject _boosterHint;
        [SerializeField] TMP_Text _boosterHintText;

        public void Bind(Action<Button, UnityAction> bind, Action<BoosterType> onBooster)
        {
            for (int i = 0; i < _boosters.Length; i++)
            {
                var type = (BoosterType)i;
                bind(_boosters[i].button, () => onBooster(type));
            }
        }

        public void SetBooster(BoosterType type, bool locked, int unlockLevel, int count, bool usable, bool selected, bool buyable)
        {
            int i = (int)type;
            if (i >= 0 && i < _boosters.Length)
                _boosters[i].Show(locked, unlockLevel, count, usable, selected, buyable);
        }

        public void SetHint(string text)
        {
            if (_boosterHint == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (_boosterHint.activeSelf != show) _boosterHint.SetActive(show);
            if (show && _boosterHintText.text != text) _boosterHintText.text = text;
        }
    }
}
