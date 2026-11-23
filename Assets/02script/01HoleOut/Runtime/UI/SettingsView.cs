using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    [Serializable]
    internal sealed class SettingsView
    {
        [SerializeField] GameObject _settingsPanel;
        [SerializeField] Button _settings, _closeSettings, _prev, _next, _retry;
        [Tooltip("Nút về Home trong bảng Cài đặt.")]
        [SerializeField] Button _home;
        [SerializeField] TMP_Text _settingsLevel;
        [Tooltip("Chỉ hiện trong Editor/bản dev (ví dụ bộ chọn màn trong Cài đặt).")]
        [SerializeField] GameObject[] _devOnly = Array.Empty<GameObject>();

        public bool Visible => _settingsPanel != null && _settingsPanel.activeSelf;

        public void Bind(Action<Button, UnityAction> bind, UnityAction onPrev, UnityAction onNext, UnityAction onRetry, UnityAction onHome)
        {
            if (!Debug.isDebugBuild)
            {
                foreach (var item in _devOnly)
                    if (item) item.SetActive(false);
            }

            bind(_settings, () => _settingsPanel.SetActive(true));
            bind(_closeSettings, Hide);
            bind(_prev, onPrev);
            bind(_next, onNext);
            bind(_retry, onRetry);
            bind(_home, onHome);
        }

        public void Hide()
        {
            if (Visible) _settingsPanel.SetActive(false);
        }

        public void SetLevel(string label)
        {
            if (_settingsLevel != null && _settingsLevel.text != label)
                _settingsLevel.text = label;
        }
    }
}
