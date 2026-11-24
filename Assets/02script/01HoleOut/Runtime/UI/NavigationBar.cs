using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Thanh tab ở đáy Home: tab đang chọn rộng hơn, icon nổi lên, hiện tên và có vệt sáng trượt theo.
    /// Hành động của từng tab (mở popup…) gắn trên Button của tab đó.
    /// </summary>
    public sealed class NavigationBar : MonoBehaviour
    {
        [Tooltip("Tab chọn sẵn khi mở Home và khi đóng popup.")]
        [SerializeField] int homeIndex = 1;
        [Tooltip("Tab đang chọn rộng hơn các tab khác theo hệ số này.")]
        [Min(1f)] [SerializeField] float selectedExpand = 1.2f;
        [Min(0.01f)] [SerializeField] float duration = 0.3f;
        [Tooltip("Vệt sáng nằm sau tab đang chọn.")]
        [SerializeField] RectTransform cursor;
        [Tooltip("Dải chứa các tab; chiều rộng được chia cho các tab.")]
        [SerializeField] RectTransform strip;
        [SerializeField] NavigationCell[] tabs;

        int _current = -1, _previous = -1;
        float _progress = 1f;
        float _width, _cursorFrom;

        void Awake()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].Button.onClick.AddListener(() => Select(index, true));
                tabs[i].SetLine(i < tabs.Length - 1);
            }
        }

        void Start() => Select(homeIndex, false);

        public void SelectHome() => Select(homeIndex, true);

        public void Select(int index, bool animate)
        {
            if (index < 0 || index >= tabs.Length || index == _current) return;
            _previous = animate ? _current : -1;
            _current = index;
            if (cursor) _cursorFrom = cursor.anchoredPosition.x;
            _progress = animate && _previous >= 0 ? 0f : 1f;
            Layout();
        }

        void Update()
        {
            if (_current < 0) return;
            bool resized = !Mathf.Approximately(strip.rect.width, _width);
            if (_progress >= 1f && !resized) return;
            _progress = Mathf.Min(1f, _progress + Time.unscaledDeltaTime / duration);
            Layout();
        }

        void Layout()
        {
            _width = strip.rect.width;
            int count = tabs.Length;
            if (count == 0 || _width <= 0f) return;
            float selectedWidth = count > 1 ? _width / count * selectedExpand : _width;
            float normalWidth = count > 1 ? (_width - selectedWidth) / (count - 1) : _width;
            float eased = 1f - Mathf.Pow(1f - _progress, 4f);
            float pop = Mathf.Sin(_progress * Mathf.PI) * (1f - _progress);

            float left = strip.rect.xMin, cursorX = 0f;
            for (int i = 0; i < count; i++)
            {
                float amount = i == _current ? (_previous < 0 ? 1f : eased) : i == _previous ? 1f - eased : 0f;
                float width = Mathf.Lerp(normalWidth, selectedWidth, amount);
                var rect = tabs[i].Rect;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.anchoredPosition = new Vector2(left + width * 0.5f - strip.rect.center.x, rect.anchoredPosition.y);
                tabs[i].Apply(amount, i == _current ? pop : 0f);
                if (i == _current) cursorX = rect.anchoredPosition.x;
                left += width;
            }

            if (!cursor) return;
            cursor.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selectedWidth);
            float x = _previous < 0 ? cursorX : Mathf.Lerp(_cursorFrom, cursorX, eased);
            cursor.anchoredPosition = new Vector2(x, cursor.anchoredPosition.y);
        }
    }
}
