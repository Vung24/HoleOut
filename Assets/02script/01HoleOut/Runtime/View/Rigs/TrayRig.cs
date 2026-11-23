using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Khay bo góc theo kích thước thật, giữ nguyên bề rộng viền và bán kính góc.</summary>
    public class TrayRig : MonoBehaviour
    {
        public MeshFilter baseLayer, rimLayer, surfaceLayer;
        [Min(0.02f)] public float cornerRadius = 0.22f;
        [Min(0.01f)] public float borderWidth = 0.11f;
        readonly List<Mesh> _ownedMeshes = new List<Mesh>();

        public void HideFill()
        {
            if (baseLayer) baseLayer.gameObject.SetActive(false);
            if (surfaceLayer) surfaceLayer.gameObject.SetActive(false);
        }

        public void SetBounds(float minX, float maxX, float minZ, float maxZ)
        {
            transform.localPosition = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
            transform.localScale = Vector3.one;
            ReleaseMeshes();
            float width = Mathf.Max(0.1f, maxX - minX), depth = Mathf.Max(0.1f, maxZ - minZ);
            float border = Mathf.Min(borderWidth, Mathf.Min(width, depth) * 0.2f);
            float radius = Mathf.Clamp(cornerRadius, border, Mathf.Min(width, depth) / 2);
            Assign(baseLayer, Panel(width, depth, radius, -0.095f, -0.04f, 0.025f, 0));
            Assign(rimLayer, Panel(width, depth, radius, -0.055f, 0.035f, 0.025f, border));
            Assign(surfaceLayer, Panel(width - border * 2, depth - border * 2,
                Mathf.Max(0.01f, radius - border), -0.055f, -0.005f, 0.012f, 0));
        }

        void Assign(MeshFilter target, Mesh mesh)
        {
            _ownedMeshes.Add(mesh);
            if (target) target.sharedMesh = mesh;
        }

        void ReleaseMeshes()
        {
            foreach (var mesh in _ownedMeshes)
                if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            _ownedMeshes.Clear();
        }

        void OnDestroy() => ReleaseMeshes();

        /// <summary>Mặt bo góc với cạnh vát; border > 0 tạo viền rỗng.</summary>
        public static Mesh Panel(float width, float depth, float radius, float bottom, float top, float bevel, float border) =>
            Panel(width, depth, new Vector4(radius, radius, radius, radius), bottom, top, bevel, border);

        /// <summary>Như trên nhưng bán kính riêng từng góc theo thứ tự (+x,+z), (-x,+z), (-x,-z), (+x,-z).</summary>
        public static Mesh Panel(float width, float depth, Vector4 radii, float bottom, float top, float bevel, float border)
        {
            const int steps = 8, count = (steps + 1) * 4;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddRing(width, depth, 0, bottom);
            AddRing(width, depth, 0, top - bevel);
            AddRing(width - bevel * 2, depth - bevel * 2, bevel, top);
            if (border > 0)
            {
                AddRing(width - border * 2, depth - border * 2, border, top);
                AddRing(width - border * 2, depth - border * 2, border, bottom);
            }
            int rings = vertices.Count / count;
            for (int ring = 0; ring < rings - 1; ring++)
                for (int i = 0; i < count; i++)
                {
                    int a = ring * count + i, b = ring * count + (i + 1) % count;
                    int c = a + count, d = b + count;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            if (border <= 0)
            {
                int center = vertices.Count;
                vertices.Add(new Vector3(0, top, 0));
                for (int i = 0; i < count; i++)
                {
                    triangles.Add(center); triangles.Add(2 * count + (i + 1) % count); triangles.Add(2 * count + i);
                }
            }
            var mesh = new Mesh { name = border > 0 ? "RoundedTrayRim" : "RoundedTrayPanel" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;

            // inset: lượng co vào so với khối ngoài, bán kính góc giảm theo tương ứng.
            void AddRing(float w, float d, float inset, float y)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    float r = Mathf.Max(0.001f, radii[corner] - inset);
                    float cx = (corner == 0 || corner == 3 ? 1 : -1) * (w / 2 - r);
                    float cz = (corner < 2 ? 1 : -1) * (d / 2 - r);
                    for (int step = 0; step <= steps; step++)
                    {
                        float angle = (corner * 90f + step * 90f / steps) * Mathf.Deg2Rad;
                        vertices.Add(new Vector3(cx + Mathf.Cos(angle) * r, y, cz + Mathf.Sin(angle) * r));
                    }
                }
            }
        }
    }
}
