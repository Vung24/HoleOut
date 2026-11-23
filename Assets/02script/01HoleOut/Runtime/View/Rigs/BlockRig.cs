using TMPro;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Prefab khối kéo được: thân bo tròn, viền chọn và số sức chứa. Kích thước theo số ô của khối.</summary>
    public class BlockRig : MonoBehaviour
    {
        const float Inset = 0.16f;
        const float BodyHeight = 0.62f;
        const int ArcSteps = 8;
        // Khối màu sáng (trắng, vàng nhạt...) dùng chữ tối để số không chìm vào thân khối.
        const float LightBodyLuminance = 0.8f;
        static readonly Color DarkLabel = new Color32(30, 30, 60, 255);

        public MeshFilter body;
        public MeshRenderer bodyRenderer;
        public LineRenderer outline;
        public TMP_Text label;

        int _shownCapacity = int.MinValue;
        bool _shownMystery;

        void Awake()
        {
            // Viền chữ tạo material riêng cho từng chữ nên chỉ bật lúc chạy, không lưu vào prefab.
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(30, 30, 60, 255);
        }

        public void Setup(int w, int h)
        {
            var size = body.sharedMesh.bounds.size;
            float fx = w * BoardSpace.BoardCell - Inset, fz = h * BoardSpace.BoardCell - Inset;
            // Mesh khối nằm xoay trong prefab: trục y của mesh là chiều sâu z trên bàn.
            body.transform.localScale = new Vector3(fx / size.x, fz / size.y, BodyHeight / size.z);
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(1.2f, fx), 1);
            BuildOutline(bodyRenderer.bounds);
            outline.enabled = false;
            _shownCapacity = int.MinValue;
        }

        public void SetSelected(bool selected)
        {
            if (outline.enabled != selected) outline.enabled = selected;
        }

        public void SetLook(Material material, Color bodyColor, bool mystery, int capacity)
        {
            if (bodyRenderer.sharedMaterial != material) bodyRenderer.sharedMaterial = material;
            var labelColor = bodyColor.grayscale >= LightBodyLuminance ? DarkLabel : Color.white;
            if (label.color != labelColor) label.color = labelColor;
            if (_shownMystery == mystery && (mystery || _shownCapacity == capacity)) return;
            _shownMystery = mystery;
            _shownCapacity = capacity;
            label.text = mystery ? "?" : capacity.ToString();
        }

        /// <summary>Viền trắng bo góc bao quanh mặt trên khối.</summary>
        void BuildOutline(Bounds bounds)
        {
            var center = transform.InverseTransformPoint(bounds.center);
            float halfX = bounds.extents.x + 0.015f;
            float halfZ = bounds.extents.z + 0.015f;
            float radius = Mathf.Min(0.16f, Mathf.Min(halfX, halfZ) * 0.45f);
            float top = bounds.max.y - transform.position.y + 0.025f;
            outline.positionCount = 4 * (ArcSteps + 1);
            for (int corner = 0; corner < 4; corner++)
            {
                float signX = corner == 0 || corner == 3 ? 1 : -1;
                float signZ = corner < 2 ? 1 : -1;
                var arcCenter = new Vector3(center.x + signX * (halfX - radius), top, center.z + signZ * (halfZ - radius));
                for (int i = 0; i <= ArcSteps; i++)
                {
                    float angle = (corner * 90f + i * 90f / ArcSteps) * Mathf.Deg2Rad;
                    outline.SetPosition(corner * (ArcSteps + 1) + i,
                        arcCenter + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                }
            }
        }
    }
}
