#if UNITY_EDITOR
using System.Collections.Generic;
#endif
using TMPro;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Mảnh sprite ghép thân khối trên bàn; thứ tự trùng với <see cref="VisualsSO.ColorVisual.blockParts"/>.</summary>
    public enum BoardBlockPart
    {
        ConvexUpLeft, LinearXUp, ConvexUpRight,
        LinearYLeft, Close, LinearYRight,
        ConvexDownLeft, LinearXDown, ConvexDownRight,
        ConcaveUpLeft, ConcaveUpRight, ConcaveDownLeft, ConcaveDownRight,
        ShadowDownLeft, ShadowX, ShadowDownRight,
    }

    /// <summary>
    /// Hiển thị màu, sức chứa và trạng thái kéo trên prefab khối của bàn.
    /// </summary>
    public class BoardBlockRig : MonoBehaviour
    {
        const float LightBodyLuminance = 0.8f;
        // Khối đang kéo vẽ trên mọi khối khác và hơi phóng to.
        const int SelectedSortingOffset = 100;
        const float SelectedScale = 1.06f;

        public Vector2Int size = Vector2Int.one;
        [Tooltip("Gốc chứa toàn bộ hình của khối; phóng to quanh tâm khi đang kéo.")]
        public Transform visual;
        [SerializeField] Transform holeRoot;
        public SpriteRenderer[] parts;
        public BoardBlockPart[] kinds;
        public TMP_Text label;
        [Tooltip("Thành ngoài và mặt vành hố; tô theo màu khối.")]
        public Renderer[] rimRenderers;
        [Tooltip("Thành trong và đáy hố; tô theo màu khối với độ tối từ mesh.")]
        public Renderer[] interiorRenderers;
        [Range(0f, 1f), Tooltip("Tỉ lệ pha trắng vào màu vành hố.")]
        public float rimLighten = 0.35f;

        public MeshFilter solid;
        [SerializeField] MeshRenderer solidRenderer;
        [SerializeField] Renderer[] selectionRenderers;
        [SerializeField, Tooltip("Tâm miệng hố trong hệ tọa độ của Visual.")]
        Vector3 holeCenter;
        [SerializeField, Min(0.01f)] float labelDiameter;
        [SerializeField, Min(0f), Tooltip("Kênh xanh lá của màu thân trong mesh bo góc, dùng để lấy sắc độ. Đặt 0 cho material dùng màu mặt riêng.")]
        float baseVertexGreen;
        const float RoundedRimRise = 0.035f;
        MaterialPropertyBlock _solidColor;
        MaterialPropertyBlock _holeColor;

        [Header("Hình khối dùng khi tạo mẫu trong Editor")]
        [Min(0.08f)] public float bodyHeight = 0.25f;
        [Range(0.01f, 0.08f)] public float bevelWidth = 0.045f;
        [Tooltip("Lệch tâm hố so với tâm khối, theo trục X và Z.")]
        public Vector2 holeOffset = new Vector2(0, -0.04f);
        [Tooltip("Cỡ hố trên khối một ô.")]
        [Range(0.5f, 1f)] public float holeScale = 0.88f;
        [Tooltip("Cỡ hố tăng thêm theo mỗi ô của cạnh ngắn (khối 2×2 có hố lớn hơn).")]
        [Range(0f, 1f)] public float holeGrowthPerCell = 0.6f;

        [Header("Mặt khối")]
        [Tooltip("Khoảng từ mép mặt trên vào viền tấm lõm.")]
        [Range(0f, 0.15f)] public float panelInset = 0.05f;
        [Range(0.005f, 0.04f)] public float lineWidth = 0.016f;
        [Tooltip("Độ sáng viền tấm lõm và rãnh giữa các ô so với mặt khối.")]
        [Range(0.5f, 1f)] public float lineShade = 0.84f;

        public const float HoleOpeningRadius = 0.255f;
        public const float HoleRimHeight = 0.11f;
        public const float HoleRimRadius = 0.34f;

        public Vector3 HoleWorldPosition => visual.TransformPoint(holeCenter);

        // Mọi khối dùng chung một cỡ số: cỡ vừa khít số ba chữ số trong hố khối một ô.
        const string LabelReferenceText = "888";
        const float LabelFitDiameter = 0.4f;
        static float _labelScale;

        Bounds MeasureLabel()
        {
            label.ForceMeshUpdate();
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0);
            var textInfo = label.textInfo;
            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;
                min = Vector3.Min(min, character.bottomLeft);
                max = Vector3.Max(max, character.topRight);
            }
            return float.IsInfinity(min.x) ? new Bounds() : new Bounds((min + max) * 0.5f, max - min);
        }

        float LabelScale()
        {
            if (_labelScale > 0) return _labelScale;
            string text = label.text;
            label.text = LabelReferenceText;
            float diagonal = MeasureLabel().size.magnitude;
            label.text = text;
            _labelScale = Mathf.Min(1, LabelFitDiameter / Mathf.Max(diagonal, 0.001f));
            return _labelScale;
        }

        void UpdateLabelLayout()
        {
            float scale = LabelScale();
            var bounds = MeasureLabel();
            if (bounds.size == Vector3.zero) return;
            // Đặt chữ ngay dưới miệng hố để vành không che số khi nhìn nghiêng.
            var labelTransform = label.transform;
            labelTransform.localScale = Vector3.one * scale;
            labelTransform.localPosition = holeCenter - labelTransform.localRotation * (bounds.center * scale);
        }

#if UNITY_EDITOR
        const int HoleSegments = 64;
        const float BodyHalfWidth = 0.46f;
        const float CornerRadius = 0.10f;
        const float PanelCornerOffset = 0.17f;
        const float LineLift = 0.003f;
        const float LabelClearance = 0.025f;

        /// <summary>Tạo mesh cho một prefab mới trong Editor.</summary>
        public void BuildSolidGeometry(Transform hole, Material material)
        {
            holeRoot = hole;
            var boxes = GetComponents<BoxCollider>();
            // Tâm collider ở cạnh ngoài có thể lệch 0,02 ô so với tâm ô lưới.
            var cells = new List<Vector3>();
            foreach (var box in boxes)
                cells.Add(new Vector3(Mathf.Round(box.center.x * 2) * 0.5f, 0, Mathf.Round(box.center.z * 2) * 0.5f));
            Vector3 min = cells[0], max = cells[0];
            foreach (var cell in cells) { min = Vector3.Min(min, cell); max = Vector3.Max(max, cell); }
            int columns = Mathf.RoundToInt(max.x - min.x) + 1, rows = Mathf.RoundToInt(max.z - min.z) + 1;
            bool rectangular = cells.Count == columns * rows;

            // Khối chữ nhật: một hố ở tâm, lớn dần theo cạnh ngắn. Hình khác giữ hố trong ô trên cùng bên trái.
            Vector3 holeCenter;
            float scale = holeScale;
            if (rectangular)
            {
                holeCenter = (min + max) * 0.5f;
                scale *= 1 + (Mathf.Min(columns, rows) - 1) * holeGrowthPerCell;
            }
            else
            {
                holeCenter = cells[0];
                foreach (var cell in cells)
                    if (cell.z > holeCenter.z + 0.1f || (Mathf.Abs(cell.z - holeCenter.z) < 0.1f && cell.x < holeCenter.x)) holeCenter = cell;
            }
            holeCenter += new Vector3(holeOffset.x, bodyHeight, holeOffset.y);
            hole.localPosition = holeCenter + Vector3.up * 0.05f;
            hole.localScale = Vector3.one * scale;
            float cutRadius = HoleOpeningRadius * scale + 0.0014f;
            this.holeCenter = hole.localPosition + Vector3.up * (HoleRimHeight * hole.localScale.y - 0.01f);
            labelDiameter = 2 * (HoleOpeningRadius * hole.localScale.x - LabelClearance);
            label.color = Color.white;
            label.rectTransform.sizeDelta = new Vector2(0.72f, 0.4f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 3.2f;
            UpdateLabelLayout();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float shade)
            {
                int i = vertices.Count;
                vertices.AddRange(new[] { a, b, c, d });
                for (int k = 0; k < 4; k++) colors.Add(new Color(shade, shade, shade, 1));
                triangles.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            bool HasCell(Vector3 position)
            {
                foreach (var other in cells)
                    if (Mathf.Abs(other.x - position.x) < 0.1f && Mathf.Abs(other.z - position.z) < 0.1f) return true;
                return false;
            }
            // Dựng một mảnh thân: thành đứng, mặt vát và mặt trên (khoét hố nếu tâm hố nằm trong mảnh).
            void Piece(Vector3 origin, float halfX, float halfZ, bool left, bool right, bool down, bool up)
            {
                float x0 = left ? -0.5f : -halfX, x1 = right ? 0.5f : halfX;
                float z0 = down ? -0.5f : -halfZ, z1 = up ? 0.5f : halfZ;
                var lower = new List<Vector3>();
                var upper = new List<Vector3>();
                // Các góc nối ô giữ vuông; chỉ bo góc ngoài của toàn bộ hình.
                for (int corner = 0; corner < 4; corner++)
                {
                    bool positiveX = corner == 0 || corner == 3;
                    bool positiveZ = corner < 2;
                    bool exposedX = positiveX ? !right : !left;
                    bool exposedZ = positiveZ ? !up : !down;
                    float radius = exposedX && exposedZ ? CornerRadius : 0;
                    float cx = positiveX ? x1 - radius : x0 + radius;
                    float cz = positiveZ ? z1 - radius : z0 + radius;
                    float insetX = exposedX ? bevelWidth : 0, insetZ = exposedZ ? bevelWidth : 0;
                    for (int step = 0; step <= 5; step++)
                    {
                        float angle = (corner * 90 + step * 18) * Mathf.Deg2Rad;
                        var p = new Vector3(cx + Mathf.Cos(angle) * radius, 0, cz + Mathf.Sin(angle) * radius);
                        lower.Add(origin + p);
                        var q = radius > 0
                            ? new Vector3(cx + Mathf.Cos(angle) * (radius - bevelWidth), bodyHeight, cz + Mathf.Sin(angle) * (radius - bevelWidth))
                            : new Vector3(p.x + (positiveX ? -insetX : insetX), bodyHeight, p.z + (positiveZ ? -insetZ : insetZ));
                        upper.Add(origin + q);
                    }
                }
                bool hasHole = holeCenter.x > origin.x + x0 && holeCenter.x < origin.x + x1
                    && holeCenter.z > origin.z + z0 && holeCenter.z < origin.z + z1;
                var topCenter = hasHole ? holeCenter : origin + Vector3.up * bodyHeight;
                for (int i = 0; i < lower.Count; i++)
                {
                    int j = (i + 1) % lower.Count;
                    var a = lower[i]; var b = lower[j];
                    bool internalEdge = (left && Mathf.Abs(a.x - origin.x - x0) < 0.001f && Mathf.Abs(b.x - origin.x - x0) < 0.001f)
                        || (right && Mathf.Abs(a.x - origin.x - x1) < 0.001f && Mathf.Abs(b.x - origin.x - x1) < 0.001f)
                        || (down && Mathf.Abs(a.z - origin.z - z0) < 0.001f && Mathf.Abs(b.z - origin.z - z0) < 0.001f)
                        || (up && Mathf.Abs(a.z - origin.z - z1) < 0.001f && Mathf.Abs(b.z - origin.z - z1) < 0.001f);
                    if (!internalEdge)
                    {
                        var shoulderA = a + Vector3.up * (bodyHeight - bevelWidth);
                        var shoulderB = b + Vector3.up * (bodyHeight - bevelWidth);
                        Quad(a, b, shoulderB, shoulderA, 0.58f);
                        Quad(shoulderA, shoulderB, upper[j], upper[i], 0.82f);
                    }
                    if (hasHole)
                    {
                        var start = upper[i] - topCenter;
                        var end = upper[j] - topCenter;
                        float angle = Mathf.Atan2(start.x * end.z - start.z * end.x, Vector3.Dot(start, end));
                        int steps = Mathf.Max(1, Mathf.CeilToInt(angle / (Mathf.PI * 2 / HoleSegments)));
                        for (int step = 0; step < steps; step++)
                        {
                            var outerA = Vector3.Lerp(upper[i], upper[j], (float)step / steps);
                            var outerB = Vector3.Lerp(upper[i], upper[j], (float)(step + 1) / steps);
                            var innerA = topCenter + (outerA - topCenter).normalized * cutRadius;
                            var innerB = topCenter + (outerB - topCenter).normalized * cutRadius;
                            Quad(outerA, outerB, innerB, innerA, 1);
                        }
                    }
                    else Quad(upper[i], upper[j], topCenter, topCenter, 1);
                }
            }

            if (rectangular)
            {
                // Cả khối là một mảnh liền để mặt trên không bị chia cắt quanh hố.
                var center = (min + max) * 0.5f;
                float halfX = (max.x - min.x) * 0.5f + BodyHalfWidth, halfZ = (max.z - min.z) * 0.5f + BodyHalfWidth;
                Piece(center, halfX, halfZ, false, false, false, false);
                AddPanelLines(center, halfX, halfZ, columns, rows, min, HoleRimRadius * scale);
            }
            else
                foreach (var cell in cells)
                    Piece(cell, BodyHalfWidth, BodyHalfWidth, HasCell(cell + Vector3.left), HasCell(cell + Vector3.right),
                        HasCell(cell + Vector3.back), HasCell(cell + Vector3.forward));
            foreach (var box in boxes)
            {
                box.center = new Vector3(box.center.x, bodyHeight * 0.5f, box.center.z);
                box.size = new Vector3(box.size.x, bodyHeight, box.size.z);
            }

            // Viền tấm lõm chạy sát mép mặt trên; rãnh mảnh giữa các ô dừng trước vành hố.
            void AddPanelLines(Vector3 center, float halfX, float halfZ, int cols, int rowCount, Vector3 firstCell, float rimRadius)
            {
                float y = bodyHeight + LineLift;
                float outerRadius = PanelCornerOffset - bevelWidth - panelInset;
                if (outerRadius > lineWidth)
                {
                    var outer = RoundRect(center, halfX, halfZ, outerRadius, y);
                    var inner = RoundRect(center, halfX, halfZ, outerRadius - lineWidth, y);
                    for (int i = 0; i < outer.Count; i++)
                    {
                        int j = (i + 1) % outer.Count;
                        Quad(outer[i], outer[j], inner[j], inner[i], lineShade);
                    }
                }
                float reachX = halfX - bevelWidth - panelInset - lineWidth, reachZ = halfZ - bevelWidth - panelInset - lineWidth;
                float clearance = rimRadius + 0.03f;
                for (int c = 1; c < cols; c++)
                {
                    float x = firstCell.x + c - 0.5f;
                    Groove(new Vector3(x, y, center.z - reachZ), new Vector3(x, y, center.z + reachZ), clearance);
                }
                for (int r = 1; r < rowCount; r++)
                {
                    float z = firstCell.z + r - 0.5f;
                    Groove(new Vector3(center.x - reachX, y, z), new Vector3(center.x + reachX, y, z), clearance);
                }
            }
            List<Vector3> RoundRect(Vector3 center, float halfX, float halfZ, float radius, float y)
            {
                var points = new List<Vector3>();
                for (int corner = 0; corner < 4; corner++)
                {
                    float cx = center.x + (corner == 0 || corner == 3 ? halfX - PanelCornerOffset : PanelCornerOffset - halfX);
                    float cz = center.z + (corner < 2 ? halfZ - PanelCornerOffset : PanelCornerOffset - halfZ);
                    for (int step = 0; step <= 5; step++)
                    {
                        float angle = (corner * 90 + step * 18) * Mathf.Deg2Rad;
                        points.Add(new Vector3(cx + Mathf.Cos(angle) * radius, y, cz + Mathf.Sin(angle) * radius));
                    }
                }
                return points;
            }
            void Groove(Vector3 from, Vector3 to, float clearance)
            {
                // Cắt đoạn thẳng quanh hình tròn vành hố.
                var direction = (to - from).normalized;
                float length = Vector3.Distance(from, to);
                var toHole = new Vector3(holeCenter.x - from.x, 0, holeCenter.z - from.z);
                float along = Vector3.Dot(toHole, direction);
                float side = (toHole - direction * along).magnitude;
                if (side >= clearance) { Line(from, to); return; }
                float gap = Mathf.Sqrt(clearance * clearance - side * side);
                if (along - gap > 0) Line(from, from + direction * Mathf.Min(along - gap, length));
                if (along + gap < length) Line(from + direction * Mathf.Max(along + gap, 0), to);
            }
            void Line(Vector3 from, Vector3 to)
            {
                var direction = (to - from).normalized;
                var normal = new Vector3(direction.z, 0, -direction.x) * (lineWidth * 0.5f);
                Quad(from - normal, from + normal, to + normal, to - normal, lineShade);
            }
            var go = new GameObject("SolidBody", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visual, false);
            solid = go.GetComponent<MeshFilter>();
            solidRenderer = go.GetComponent<MeshRenderer>();
            solidRenderer.sharedMaterial = material;
            var mesh = new Mesh { name = name + "BodyMesh" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            solid.sharedMesh = mesh;
            foreach (var part in parts) part.enabled = false;
            selectionRenderers = GetComponentsInChildren<Renderer>(true);
        }
#endif

        void SetSolidColor(Color color, Material pictureMaterial = null)
        {
            if (_solidColor == null) _solidColor = new MaterialPropertyBlock();
            bool hasPictureColors = pictureMaterial && pictureMaterial.shader.name == "HoleOut/Picture Cell";
            _solidColor.SetColor("_Color", hasPictureColors ? pictureMaterial.GetColor("_BaseColor") : color);
            _solidColor.SetColor("_HighlightColor", hasPictureColors ? pictureMaterial.GetColor("_HighlightColor") : Color.Lerp(color, Color.white, 0.4f));
            _solidColor.SetColor("_EdgeColor", hasPictureColors ? pictureMaterial.GetColor("_EdgeColor") : color * 0.82f);
            _solidColor.SetColor("_FrontColor", hasPictureColors ? pictureMaterial.GetColor("_FrontColor") : color * 0.58f);
            if (baseVertexGreen > 0)
            {
                _solidColor.SetFloat("_BaseVertexGreen", baseVertexGreen);
                _solidColor.SetFloat("_GradientHeight", solid.sharedMesh.bounds.max.y - RoundedRimRise);
                var forward = Quaternion.Inverse(solid.transform.localRotation) * Vector3.forward;
                _solidColor.SetVector("_FaceFrame", new Vector4(forward.x, forward.z, 2f / size.x, 2f / size.y));
            }
            solidRenderer.SetPropertyBlock(_solidColor);
        }

        void SetHoleColor(Color color)
        {
            // Khối sáng (trắng, vàng nhạt) dùng vành tối hơn một chút để vẫn tách khỏi mặt khối.
            var rim = color.grayscale >= LightBodyLuminance ? color * 0.82f : Color.Lerp(color, Color.white, rimLighten);
            if (_holeColor == null) _holeColor = new MaterialPropertyBlock();
            ApplyTint(rimRenderers, rim);
            ApplyTint(interiorRenderers, color);

            void ApplyTint(Renderer[] renderers, Color tint)
            {
                if (renderers == null) return;
                tint.a = 1;
                _holeColor.SetColor("_Color", tint);
                foreach (var renderer in renderers)
                    if (renderer) renderer.SetPropertyBlock(_holeColor);
            }
        }

        int[] _baseOrders;
        bool _selected;
        VisualsSO.ColorVisual _shownLook;
        int _shownCapacity = int.MinValue;
        bool _shownMystery;

        void Awake()
        {
            _baseOrders = new int[selectionRenderers.Length];
            for (int i = 0; i < selectionRenderers.Length; i++) _baseOrders[i] = selectionRenderers[i].sortingOrder;
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            for (int i = 0; i < selectionRenderers.Length; i++)
                selectionRenderers[i].sortingOrder = _baseOrders[i] + (selected ? SelectedSortingOffset : 0);
            visual.localScale = Vector3.one * (selected ? SelectedScale : 1);
        }

        /// <summary>Độ phóng khi nhấc theo tỉ lệ 0 (nằm) → 1 (đang kéo); có thể vượt nhẹ ngoài [0, 1] để nảy.</summary>
        public void SetLiftScale(float amount) => visual.localScale = Vector3.one * (1 + (SelectedScale - 1) * amount);

        public void SetLook(VisualsSO.ColorVisual look, bool mystery, int capacity)
        {
            if (look != null && look != _shownLook)
            {
                _shownLook = look;
                SetSolidColor(look.color, look.seated);
                if (holeRoot && holeRoot.gameObject.activeSelf) SetHoleColor(look.color);
            }
            if (_shownMystery == mystery && (mystery || _shownCapacity == capacity)) return;
            _shownMystery = mystery;
            _shownCapacity = capacity;
            label.text = mystery ? "?" : capacity.ToString();
            UpdateLabelLayout();
        }
    }
}
