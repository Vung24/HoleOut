using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Hoạt cảnh mở popup dùng chung: lớp phủ mờ dần lên độ đậm gốc, thẻ nảy nhẹ theo các mốc scale.
    /// Chạy bằng thời gian không phụ thuộc timeScale; bật lại object là phát lại từ đầu.
    /// </summary>
    public class PopupAnim : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RectTransform card;
        [Tooltip("Lớp phủ tối phía sau thẻ (tùy chọn).")]
        [SerializeField] Graphic dim;
        [Min(0.01f)] [SerializeField] float dimTime = 0.15f;
        [Tooltip("Các mốc scale của thẻ: bắt đầu, vượt, hụt, về 1.")]
        [SerializeField] Vector4 scaleKeys = new Vector4(0.7f, 1.08f, 0.97f, 1f);
        [Tooltip("Thời gian từng đoạn giữa các mốc scale.")]
        [SerializeField] Vector3 scaleTimes = new Vector3(0.16f, 0.1f, 0.1f);

        float _dimAlpha = -1, _start;
        bool _done;

        public void Init(RectTransform cardTransform, Graphic dimGraphic)
        {
            card = cardTransform;
            dim = dimGraphic;
        }

        void OnEnable()
        {
            if (dim && _dimAlpha < 0) _dimAlpha = dim.color.a;
            _start = Time.unscaledTime;
            _done = false;
            Apply(0);
        }

        void Update()
        {
            if (_done) return;
            float t = Time.unscaledTime - _start;
            Apply(t);
            _done = t > dimTime && t > scaleTimes.x + scaleTimes.y + scaleTimes.z;
        }

        void OnDisable()
        {
            // Bị tắt giữa chừng thì để trạng thái cuối; lần bật sau phát lại từ đầu.
            Finish();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_done && dim && eventData.pointerPressRaycast.gameObject == dim.gameObject)
                Finish();
        }

        void Finish()
        {
            _done = true;
            Apply(float.MaxValue);
        }

        void Apply(float t)
        {
            if (dim)
            {
                var c = dim.color;
                c.a = _dimAlpha * Mathf.Clamp01(t / dimTime);
                dim.color = c;
            }
            if (card) card.localScale = Vector3.one * Scale(t);
        }

        float Scale(float t)
        {
            if (t < scaleTimes.x) return Mathf.Lerp(scaleKeys.x, scaleKeys.y, EaseOut(t / scaleTimes.x));
            t -= scaleTimes.x;
            if (t < scaleTimes.y) return Mathf.Lerp(scaleKeys.y, scaleKeys.z, EaseInOut(t / scaleTimes.y));
            t -= scaleTimes.y;
            if (t < scaleTimes.z) return Mathf.Lerp(scaleKeys.z, scaleKeys.w, EaseInOut(t / scaleTimes.z));
            return scaleKeys.w;
        }

        static float EaseOut(float x) => 1 - (1 - x) * (1 - x);
        static float EaseInOut(float x) => x * x * (3 - 2 * x);
    }
}
