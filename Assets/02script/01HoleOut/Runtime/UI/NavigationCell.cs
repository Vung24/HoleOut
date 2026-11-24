using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>Một tab của thanh điều hướng; chỉ hiển thị theo mức chọn do NavigationBar truyền vào.</summary>
    public sealed class NavigationCell : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] RectTransform icon;
        [SerializeField] TMP_Text label;
        [Tooltip("Vạch chia nằm bên phải tab.")]
        [SerializeField] GameObject line;
        [Header("Icon")]
        [SerializeField] float iconNormalY = -124f;
        [SerializeField] float iconSelectedY = -43f;
        [SerializeField] float normalScale = 0.8f;
        [SerializeField] float selectedScale = 0.9f;
        [Tooltip("Icon phóng thêm một chút lúc vừa được chọn rồi thu về.")]
        [SerializeField] float selectPop = 0.12f;

        public Button Button => button;
        public RectTransform Rect => (RectTransform)transform;

        public void SetLine(bool visible)
        {
            if (line && line.activeSelf != visible) line.SetActive(visible);
        }

        /// <param name="selected">0 = thường, 1 = đang chọn.</param>
        /// <param name="pop">Độ nảy thêm (0..1) trong lúc chuyển sang trạng thái chọn.</param>
        public void Apply(float selected, float pop)
        {
            if (icon)
            {
                var position = icon.anchoredPosition;
                position.y = Mathf.LerpUnclamped(iconNormalY, iconSelectedY, selected);
                icon.anchoredPosition = position;
                icon.localScale = Vector3.one * (Mathf.Lerp(normalScale, selectedScale, selected) + selectPop * pop);
            }
            if (label)
            {
                var color = label.color;
                color.a = Mathf.Clamp01(selected * 2f - 1f);
                label.color = color;
            }
        }
    }
}
