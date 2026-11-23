using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>Nút Bật/Tắt cho một tùy chọn trong <see cref="Preferences"/>.</summary>
    public sealed class PreferenceToggle : MonoBehaviour
    {
        [SerializeField] PreferenceKey preference;
        [SerializeField] Button button;
        [SerializeField] TMP_Text value;
        [SerializeField] Color onColor = Color.white;
        [SerializeField] Color offColor = Color.gray;

        void Awake() => button.onClick.AddListener(Toggle);
        void OnEnable() => Refresh();

        void Toggle()
        {
            Preferences.Set(preference, !Preferences.Get(preference));
            Refresh();
        }

        void Refresh()
        {
            bool on = Preferences.Get(preference);
            value.text = on ? "Bật" : "Tắt";
            button.image.color = on ? onColor : offColor;
        }
    }
}
