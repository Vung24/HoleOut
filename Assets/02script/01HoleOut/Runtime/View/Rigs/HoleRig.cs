using TMPro;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Tham chiếu trên prefab hố: thân, hai cửa, ống, lớp stencil, animator và số đếm.</summary>
    public class HoleRig : MonoBehaviour
    {
        public Renderer baseRenderer;
        public Renderer door1;
        public Renderer door2;
        public Renderer cylinder;
        public Renderer bottom;
        public Renderer stencil;
        public Animator animator;
        public TMP_Text count;

        static readonly int OpenHash = Animator.StringToHash("Open");
        static readonly int CloseHash = Animator.StringToHash("Close");
        static readonly int PaletteColorId = Shader.PropertyToID("_PaletteColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int HoleFloorId = Shader.PropertyToID("_HoleFloor");

        bool _open, _closed;
        MaterialPropertyBlock _paletteProperties;

        /// <summary>Bán kính miệng hố theo đơn vị thế giới (lớp stencil cắt lỗ); 0 khi prefab không có lớp này.</summary>
        public float MouthRadius
        {
            get
            {
                if (!stencil || !stencil.gameObject.activeInHierarchy) return 0;
                var e = stencil.bounds.extents;
                return Mathf.Min(e.x, e.z);
            }
        }

        public void Spawn(VisualsSO.ColorVisual v, Material pictureMaterial = null)
        {
            gameObject.SetActive(true);
            if (v != null)
            {
                Set(baseRenderer, v.hole);
                SetAll(door1, v.holeDoors);
                SetAll(door2, v.holeDoors);
                SetAll(cylinder, v.holeCylinder);
                if (v.holeCylinder.Count > 0) Set(bottom, v.holeCylinder[0]);
                Set(stencil, v.holeStencil);
                if (stencil) stencil.gameObject.SetActive(v.holeStencil && v.holeCylinder.Count > 0);
            }
            if (animator) { animator.Rebind(); animator.Update(0); }
            ApplyPictureColor(pictureMaterial);
            if (bottom)
            {
                var properties = _paletteProperties ??= new MaterialPropertyBlock();
                bottom.GetPropertyBlock(properties);
                properties.SetFloat(HoleFloorId, 1);
                bottom.SetPropertyBlock(properties);
            }
            if (count) { count.gameObject.SetActive(true); count.alpha = 1; }
            _open = _closed = false;
            _shownCount = int.MinValue;
        }

        int _shownCount = int.MinValue;

        void ApplyPictureColor(Material pictureMaterial)
        {
            var color = Color.clear;
            if (pictureMaterial && pictureMaterial.shader.name == "HoleOut/Picture Cell")
            {
                color = pictureMaterial.GetColor(BaseColorId);
                color.a = 1;
            }
            else if (_paletteProperties == null) return;

            if (_paletteProperties == null) _paletteProperties = new MaterialPropertyBlock();
            SetPaletteColor(baseRenderer, color);
            SetPaletteColor(door1, color);
            SetPaletteColor(door2, color);
        }

        void SetPaletteColor(Renderer renderer, Color color)
        {
            if (!renderer) return;
            renderer.GetPropertyBlock(_paletteProperties);
            _paletteProperties.SetColor(PaletteColorId, color);
            renderer.SetPropertyBlock(_paletteProperties);
        }

        const float PunchDuration = 0.22f;
        float _punchStart = -1;

        /// <summary>Số còn phải nhận; số giảm thì hố nảy nhẹ.</summary>
        public void SetCount(int remaining)
        {
            if (_shownCount == remaining) return;
            if (_shownCount != int.MinValue && remaining < _shownCount) _punchStart = Time.time;
            _shownCount = remaining;
            if (count) count.text = remaining.ToString();
        }

        /// <summary>Hệ số phóng to tạm thời của cú nảy khi nhận nhân vật (1 khi không nảy).</summary>
        /// <summary>Nảy nhẹ một lần (ví dụ lúc đáp xuống khay chờ).</summary>
        public void Punch() => _punchStart = Time.time;

        public float CatchScale(float amplitude)
        {
            if (_punchStart < 0) return 1;
            float k = (Time.time - _punchStart) / PunchDuration;
            if (k >= 1) { _punchStart = -1; return 1; }
            return 1 + amplitude * Mathf.Sin(k * Mathf.PI) * (1 - k);
        }

        public void Despawn()
        {
            _punchStart = -1;
            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            if (animator) animator.SetTrigger(OpenHash);
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            if (animator) animator.SetTrigger(CloseHash);
            if (count) count.gameObject.SetActive(false);
        }

        static void Set(Renderer r, Material m) { if (r && m) r.sharedMaterial = m; }

        static void SetAll(Renderer r, System.Collections.Generic.List<Material> mats)
        {
            if (!r || mats == null || mats.Count == 0) return;
            r.sharedMaterials = mats.ToArray();
        }
    }
}
