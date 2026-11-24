using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Lớp hướng dẫn trên HUD: phủ tối màn hình, vẽ đè ảnh vật thể cần chỉ để chỉ nó sáng, ngón tay diễn mẫu thao tác kéo
    /// theo đường đi, và khung lời nhắn. Chỉ trình diễn theo dữ liệu được giao mỗi frame, không biết luật chơi.
    /// Mọi Graphic đều tắt raycast để thao tác kéo trên bàn đi xuyên qua.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] Canvas _canvas;
        [Header("Lớp tối")]
        [Tooltip("Image đen phủ toàn màn hình.")]
        [SerializeField] Image _dim;
        [Tooltip("Ảnh vật thể được làm sáng, phủ đúng khung camera chơi, nằm trên lớp tối.")]
        [SerializeField] RawImage _focus;
        [SerializeField, Range(0, 1)] float _dimAlpha = 0.72f;
        [SerializeField, Min(0.01f)] float _fadeDuration = 0.3f;
        [Header("Ngón tay")]
        [Tooltip("Gốc ngón tay; pivot đặt ở đầu ngón.")]
        [SerializeField] RectTransform _finger;
        [SerializeField] Image _fingerImage;
        [Tooltip("Vòng sáng ở đầu ngón khi nhấn.")]
        [SerializeField] Image _ring;
        [SerializeField, Min(0.01f)] float _appear = 0.3f;
        [SerializeField, Min(0.01f)] float _press = 0.3f;
        [SerializeField, Min(0.01f)] float _move = 0.9f;
        [SerializeField, Min(0.01f)] float _release = 0.45f;
        [SerializeField, Min(0)] float _pause = 0.35f;
        [Header("Lời nhắn")]
        [SerializeField] RectTransform _message;
        [SerializeField] TMP_Text _messageText;

        readonly List<Vector2> _path = new List<Vector2>();
        bool _visible, _handShown, _focusShown;
        float _dimLevel, _handTime, _messageTime;

        RectTransform Rect => (RectTransform)transform;
        Camera UiCamera => _canvas && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        // Show bật object trước khi Awake chạy, nên chỉ ẩn khi chưa được gọi hiện.
        void Awake()
        {
            if (!_visible) gameObject.SetActive(false);
        }

        /// <summary>
        /// Cập nhật mỗi frame. <paramref name="focus"/>: ảnh vật thể sáng trên lớp tối (null thì tắt lớp tối);
        /// <paramref name="fingerPath"/>: đường kéo theo pixel màn hình (null thì ẩn ngón tay).
        /// </summary>
        public void Show(string message, Texture focus, IReadOnlyList<Vector2> fingerPath)
        {
            if (!_visible)
            {
                _visible = true;
                _dimLevel = 0;
                _messageTime = 0;
                gameObject.SetActive(true);
            }
            if (_messageText && _messageText.text != message) _messageText.text = message;
            if (_message) _message.gameObject.SetActive(!string.IsNullOrEmpty(message));

            _focusShown = focus;
            if (_focus && focus && _focus.texture != focus) _focus.texture = focus;

            bool hand = fingerPath != null && fingerPath.Count >= 2;
            if (hand)
            {
                _path.Clear();
                _path.AddRange(fingerPath);
                if (!_handShown) _handTime = 0;
            }
            _handShown = hand;
        }

        public void Hide()
        {
            _visible = false;
            _handShown = false;
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _dimLevel = Mathf.MoveTowards(_dimLevel, _focusShown ? 1 : 0, dt / _fadeDuration);
            float level = Smooth(_dimLevel);
            SetAlpha(_dim, _dimAlpha * level);
            // Ảnh vật thể trùng khít vật thật bên dưới nên chỉ cần mờ theo lớp tối.
            if (_focus)
            {
                _focus.enabled = _focus.texture && level > 0;
                SetAlpha(_focus, level);
            }

            // Lời nhắn bật nảy khi mới hiện.
            _messageTime += dt;
            if (_message) _message.localScale = Vector3.one * OutBack(Mathf.Clamp01(_messageTime / 0.3f));

            if (_finger) _finger.gameObject.SetActive(_handShown);
            if (_ring) _ring.gameObject.SetActive(_handShown);
            if (_handShown) AnimateFinger(dt);
        }

        void AnimateFinger(float dt)
        {
            _handTime += dt;
            float cycle = _appear + _press + _move + _release + _pause;
            float t = _handTime % cycle;

            float along = 0, alpha = 1, scale = 1, ring = 0, ringScale = 1;
            if (t < _appear)
            {
                float k = t / _appear;
                alpha = Smooth(k);
                scale = Mathf.Lerp(1.2f, 1, Smooth(k));
            }
            else if ((t -= _appear) < _press)
            {
                float k = t / _press;
                scale = Mathf.Lerp(1, 0.88f, Smooth(k));
                ring = Smooth(k);
                ringScale = Mathf.Lerp(0.4f, 1, OutBack(k));
            }
            else if ((t -= _press) < _move)
            {
                along = Smooth(t / _move);
                scale = 0.88f;
                ring = 1;
            }
            else if ((t -= _move) < _release)
            {
                float k = t / _release;
                along = 1;
                scale = Mathf.Lerp(0.88f, 1, Smooth(Mathf.Min(1, k * 2)));
                ring = 1 - Smooth(Mathf.Min(1, k * 2));
                ringScale = Mathf.Lerp(1, 1.4f, k);
                alpha = 1 - Smooth(Mathf.Max(0, k * 2 - 1));
            }
            else
            {
                along = 1;
                alpha = 0;
            }

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(Rect, PointAlong(along), UiCamera, out var world)) return;
            _finger.position = world;
            _finger.localScale = Vector3.one * scale;
            SetAlpha(_fingerImage, alpha);
            if (_ring)
            {
                _ring.rectTransform.position = world;
                _ring.rectTransform.localScale = Vector3.one * ringScale;
                SetAlpha(_ring, ring * alpha);
            }
        }

        static void SetAlpha(Graphic g, float a)
        {
            if (!g) return;
            var c = g.color;
            c.a = a;
            g.color = c;
        }

        Vector2 PointAlong(float k)
        {
            float total = 0;
            for (int i = 1; i < _path.Count; i++) total += Vector2.Distance(_path[i - 1], _path[i]);
            float d = k * total;
            for (int i = 1; i < _path.Count; i++)
            {
                float seg = Vector2.Distance(_path[i - 1], _path[i]);
                if (d <= seg && seg > 0) return Vector2.Lerp(_path[i - 1], _path[i], d / seg);
                d -= seg;
            }
            return _path[_path.Count - 1];
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);

        static float OutBack(float x)
        {
            const float c = 1.7f;
            float u = x - 1;
            return 1 + (c + 1) * u * u * u + c * u * u;
        }
    }
}
