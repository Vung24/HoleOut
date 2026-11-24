using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Vỏ băng phủ một khối trên bàn. Chỉ phản ánh số lớp băng do mô phỏng giữ:
    /// <see cref="Crack"/> khi mất một lớp, <see cref="Shatter"/> khi hết băng.
    /// Dựng trong hệ tọa độ gốc của rig khối (1 đơn vị = 1 ô).
    /// </summary>
    public sealed class IceShell : MonoBehaviour
    {
        static readonly int CrackId = Shader.PropertyToID("_Crack");
        static readonly int FlashId = Shader.PropertyToID("_Flash");
        static Mesh _shardMesh;

        IceSettings _settings;
        MeshRenderer _renderer;
        MaterialPropertyBlock _props;
        Transform _label;
        Vector3 _labelBase;
        int _initial;
        float _crack, _crackFrom, _crackTo;
        float _animStart = -1;
        Bounds _bounds;

        /// <summary>Dựng vỏ băng theo các ô của khối (lấy từ BoxCollider của rig) và cao hơn vành hố.</summary>
        public static IceShell Create(Transform rigRoot, IReadOnlyList<Vector2> cells, float top, Transform label, IceSettings settings, int layers)
        {
            var go = new GameObject("IceShell", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(rigRoot, false);
            var shell = go.AddComponent<IceShell>();
            shell._settings = settings;
            shell._label = label;
            shell._initial = Mathf.Max(1, layers);
            // Gốc vỏ đặt ở tâm khối để nảy phồng quanh tâm.
            var center = Vector2.zero;
            foreach (var c in cells) center += c;
            center /= Mathf.Max(1, cells.Count);
            go.transform.localPosition = new Vector3(center.x, 0, center.y);
            var mesh = BuildMesh(cells, center, settings.margin, top);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var bounds = mesh.bounds;
            bounds.center += go.transform.localPosition;
            shell._bounds = bounds;
            shell._renderer = go.GetComponent<MeshRenderer>();
            shell._renderer.sharedMaterial = settings.material;
            shell._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shell._renderer.receiveShadows = false;
            shell._props = new MaterialPropertyBlock();
            shell.Apply(0, 0);
            return shell;
        }

        /// <summary>Mất một lớp: chớp sáng, nảy nhẹ, số phồng lên và vết nứt lan thêm.</summary>
        public void Crack(int layersLeft)
        {
            if (_label && _animStart >= 0) _label.localScale = _labelBase;
            _crackFrom = _crack;
            _crackTo = _settings.maxCrack * (1 - Mathf.Clamp01((float)layersLeft / _initial));
            _animStart = Time.time;
            if (_label) _labelBase = _label.localScale;
        }

        /// <summary>Hết băng: vỏ biến mất, các mảnh băng văng ra rồi tan.</summary>
        public void Shatter()
        {
            if (_label && _animStart >= 0) _label.localScale = _labelBase;
            if (_settings.shardCount > 0)
            {
                var root = transform.parent;
                var burst = new GameObject("IceShatter");
                burst.transform.SetParent(root.parent, false);
                burst.transform.SetPositionAndRotation(root.position, root.rotation);
                burst.transform.localScale = root.localScale;
                burst.AddComponent<IceShatter>().Play(_settings, _bounds, _crack);
            }
            Destroy(gameObject);
        }

        void Update()
        {
            if (_animStart < 0) return;
            float k = (Time.time - _animStart) / _settings.crackDuration;
            if (k >= 1)
            {
                _animStart = -1;
                _crack = _crackTo;
                transform.localScale = Vector3.one;
                if (_label) _label.localScale = _labelBase;
                Apply(_crack, 0);
                return;
            }
            // Phồng nhanh rồi về chậm.
            float punch = Mathf.Sin(Mathf.Min(1, k * 2.2f) * Mathf.PI) * (1 - k);
            _crack = Mathf.Lerp(_crackFrom, _crackTo, Mathf.SmoothStep(0, 1, Mathf.Min(1, k * 3)));
            transform.localScale = Scaled(_settings.shellPunch, punch);
            if (_label) _label.localScale = Vector3.Scale(_labelBase, Scaled(_settings.countPunch, punch));
            Apply(_crack, 1 - k);
        }

        Vector3 Scaled(float peak, float amount)
        {
            float s = 1 + (peak - 1) * amount;
            return new Vector3(s, s, s);
        }

        void Apply(float crack, float flash)
        {
            _props.SetFloat(CrackId, crack);
            _props.SetFloat(FlashId, flash);
            _renderer.SetPropertyBlock(_props);
        }

        void OnDestroy()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf && mf.sharedMesh) Destroy(mf.sharedMesh);
        }

        /// <summary>Mỗi ô một hộp; bỏ mặt tiếp giáp giữa hai ô của cùng khối để vỏ liền một khối trong suốt.</summary>
        static Mesh BuildMesh(IReadOnlyList<Vector2> cells, Vector2 origin, float margin, float top)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            bool Has(Vector2 c)
            {
                foreach (var o in cells) if ((o - c).sqrMagnitude < 0.01f) return true;
                return false;
            }
            var shift = new Vector3(origin.x, 0, origin.y);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                a -= shift; b -= shift; c -= shift; d -= shift;
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int k = 0; k < 4; k++) normals.Add(n);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
            }
            foreach (var c in cells)
            {
                bool l = Has(c + Vector2.left), r = Has(c + Vector2.right), d = Has(c + Vector2.down), u = Has(c + Vector2.up);
                float x0 = c.x - 0.5f - (l ? 0 : margin), x1 = c.x + 0.5f + (r ? 0 : margin);
                float z0 = c.y - 0.5f - (d ? 0 : margin), z1 = c.y + 0.5f + (u ? 0 : margin);
                const float y0 = 0.01f;
                float y1 = top;
                Face(new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), Vector3.up);
                if (!d) Face(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y0, z0), Vector3.back);
                if (!u) Face(new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.forward);
                if (!l) Face(new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x0, y1, z0), new Vector3(x0, y0, z0), Vector3.left);
                if (!r) Face(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right);
            }
            var mesh = new Mesh { name = "IceShellMesh" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Mảnh băng dùng chung: khối tám mặt nhỏ, góc cạnh như đá vụn.</summary>
        public static Mesh ShardMesh()
        {
            if (_shardMesh) return _shardMesh;
            var v = new[]
            {
                new Vector3(0, 0.6f, 0), new Vector3(0, -0.5f, 0),
                new Vector3(0.5f, 0, 0), new Vector3(-0.45f, 0, 0.1f), new Vector3(0.05f, 0, 0.5f), new Vector3(-0.1f, 0, -0.45f),
            };
            int[] t = { 0, 4, 2, 0, 3, 4, 0, 5, 3, 0, 2, 5, 1, 2, 4, 1, 4, 3, 1, 3, 5, 1, 5, 2 };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                // Quay mặt ra ngoài để không bị cull ở mặt trước.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) (b, c) = (c, b);
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
            }
            _shardMesh = new Mesh { name = "IceShardMesh" };
            _shardMesh.SetVertices(vertices);
            _shardMesh.SetTriangles(triangles, 0);
            _shardMesh.RecalculateNormals();
            _shardMesh.RecalculateBounds();
            return _shardMesh;
        }
    }

    /// <summary>Các mảnh băng văng ra từ vỏ vừa vỡ, rơi theo trọng lực, thu nhỏ rồi tự hủy.</summary>
    public sealed class IceShatter : MonoBehaviour
    {
        struct Shard
        {
            public Transform t;
            public Vector3 velocity, spin;
            public float size;
        }

        readonly List<Shard> _shards = new List<Shard>();
        IceSettings _settings;
        float _start;

        public void Play(IceSettings settings, Bounds bounds, float crack)
        {
            _settings = settings;
            _start = Time.time;
            var props = new MaterialPropertyBlock();
            props.SetFloat("_Crack", crack);
            props.SetFloat("_Flash", 0.4f);
            var center = bounds.center;
            for (int i = 0; i < settings.shardCount; i++)
            {
                var go = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                var p = new Vector3(
                    Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.center.y, bounds.max.y),
                    Random.Range(bounds.min.z, bounds.max.z));
                go.transform.localPosition = p;
                go.transform.localRotation = Random.rotation;
                go.GetComponent<MeshFilter>().sharedMesh = IceShell.ShardMesh();
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = settings.material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.SetPropertyBlock(props);
                var outward = new Vector3(p.x - center.x, 0, p.z - center.z);
                if (outward.sqrMagnitude < 1e-4f) outward = Random.insideUnitSphere;
                outward.y = 0;
                var dir = (outward.normalized + Vector3.up * Random.Range(0.8f, 1.5f)).normalized;
                _shards.Add(new Shard
                {
                    t = go.transform,
                    velocity = dir * settings.shardSpeed * Random.Range(0.7f, 1.2f),
                    spin = Random.insideUnitSphere * 720f,
                    size = settings.shardSize * Random.Range(0.6f, 1.3f),
                });
            }
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            float k = (Time.time - _start) / _settings.shatterDuration;
            if (k >= 1) { Destroy(gameObject); return; }
            float scale = 1 - k * k;
            for (int i = 0; i < _shards.Count; i++)
            {
                var s = _shards[i];
                s.velocity += Vector3.down * _settings.shardGravity * dt;
                s.t.localPosition += s.velocity * dt;
                s.t.localRotation *= Quaternion.Euler(s.spin * dt);
                s.t.localScale = Vector3.one * (s.size * scale);
                _shards[i] = s;
            }
        }
    }
}
