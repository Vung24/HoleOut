using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>Nút booster trên thanh dưới: chỉ hiển thị trạng thái HUD đưa vào.</summary>
    public class BoosterButton : MonoBehaviour
    {
        public Button button;
        [SerializeField] RectTransform _face;
        [SerializeField] GameObject _countBadge;
        [SerializeField] TMP_Text _count;
        [SerializeField] GameObject _lockBadge;
        [SerializeField] TMP_Text _lockLevel;
        [SerializeField] GameObject _selected;
        [SerializeField] CanvasGroup _group;

        const float DisabledAlpha = 0.55f;

        /// <param name="unlockLevel">Màn mở khóa; chỉ hiện khi <paramref name="locked"/>.</param>
        /// <param name="buyable">Hết lượt nhưng mua được bằng coin: huy hiệu hiện "+" và nút vẫn bấm được.</param>
        public void Show(bool locked, int unlockLevel, int count, bool usable, bool selected, bool buyable = false)
        {
            _lockBadge.SetActive(locked);
            _countBadge.SetActive(!locked);
            if (locked) SetText(_lockLevel, "Màn " + unlockLevel);
            else SetText(_count, count > 0 || !buyable ? count.ToString() : "+");
            button.interactable = !locked && (count > 0 || buyable) && (usable || selected);
            _group.alpha = button.interactable ? 1 : DisabledAlpha;
            if (_selected.activeSelf != selected) _selected.SetActive(selected);
            // Nút đang chọn nhún nhẹ để báo đang chờ chạm mục tiêu.
            float pulse = selected ? 1 + 0.05f * Mathf.Sin(Time.unscaledTime * 8) : 1;
            _face.localScale = Vector3.one * pulse;
        }

        static void SetText(TMP_Text t, string s) { if (t.text != s) t.text = s; }
    }
}
