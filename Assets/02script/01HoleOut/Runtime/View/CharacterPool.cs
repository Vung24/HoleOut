using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Pool nhân vật dùng chung cho tranh và đường chạy:
    /// - nhân vật có xương (đứng, chạy, nhảy) lấy từ <see cref="PictureVisuals.AgentPrefab"/>;
    /// - ô ngồi là MeshRenderer nhẹ dựng từ mesh ngồi của prefab (shader toon tương thích SRP Batcher).
    /// </summary>
    public sealed class CharacterPool
    {
        readonly PictureVisuals _visuals;
        readonly Transform _poolRoot;
        readonly GameObject _seatedTemplate;
        readonly Stack<AgentRig> _agents = new Stack<AgentRig>();
        readonly Stack<MeshRenderer> _seated = new Stack<MeshRenderer>();
        readonly MaterialPropertyBlock _seatedBlock = new MaterialPropertyBlock();

        /// <summary>Bề ngang và chiều cao mesh ngồi ở tỉ lệ 1, dùng để quy đổi theo cỡ ô.</summary>
        public float SeatedFootprint { get; }
        public float SeatedHeight { get; }
        public Transform PoolRoot => _poolRoot;

        public CharacterPool(Transform root, PictureVisuals visuals)
        {
            _visuals = visuals;
            _poolRoot = new GameObject("AgentPool").transform;
            _poolRoot.SetParent(root, false);

            // Mẫu khối ngồi: gốc tại chân nhân vật, con giữ biến đổi cục bộ như trong prefab.
            var src = visuals.AgentPrefab.seated;
            var mesh = src.GetComponent<MeshFilter>().sharedMesh;
            _seatedTemplate = new GameObject("SeatedTemplate");
            _seatedTemplate.SetActive(false);
            _seatedTemplate.transform.SetParent(root, false);
            var child = new GameObject("Seated", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(_seatedTemplate.transform, false);
            child.transform.localPosition = src.transform.localPosition;
            child.transform.localRotation = src.transform.localRotation;
            child.transform.localScale = src.transform.localScale;
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            child.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SeatedFootprint = mesh.bounds.size.x * src.transform.localScale.x;
            SeatedHeight = mesh.bounds.size.z * src.transform.localScale.z;
        }

        public AgentRig AcquireAgent(string color, Transform parent, float scale)
        {
            var rig = _agents.Count > 0 ? _agents.Pop() : Object.Instantiate(_visuals.AgentPrefab, _poolRoot);
            rig.transform.SetParent(parent, false);
            rig.transform.localScale = Vector3.one * scale;
            rig.Spawn(_visuals.Palette.CharacterMaterial(color));
            return rig;
        }

        public void ReleaseAgent(AgentRig rig)
        {
            if (rig == null) return;
            rig.Despawn();
            rig.transform.SetParent(_poolRoot, false);
            _agents.Push(rig);
        }

        /// <summary>Ô ngồi; gốc biến đổi là <c>transform.parent</c> của renderer trả về.</summary>
        public MeshRenderer AcquireSeated(string color, Transform parent, Vector3 scale)
        {
            MeshRenderer r;
            if (_seated.Count > 0) r = _seated.Pop();
            else
            {
                // The template is built at runtime; resolve the new instance once and keep it in the pool.
                var go = Object.Instantiate(_seatedTemplate, _poolRoot);
                r = go.GetComponentInChildren<MeshRenderer>(true);
            }
            var root = r.transform.parent;
            root.SetParent(parent, false);
            root.localScale = scale;
            root.localRotation = BoardSpace.FaceCamera;
            // Ô tranh và nhân vật dùng cùng material để giữ màu khi đổi tư thế.
            var mat = _visuals.Palette.CharacterMaterial(color);
            if (r.sharedMaterial != mat) r.sharedMaterial = mat;
            // Chỉ ghi đè trên renderer này (material dùng chung không đổi):
            // tắt viền (tạo đường đậm nhạt giữa các ô sát nhau), vùng tối cùng màu,
            // bỏ ánh sáng gián tiếp và fresnel để mặt phẳng tranh không bị nhạt màu.
            r.GetPropertyBlock(_seatedBlock);
            _seatedBlock.SetFloat("_OutlineWidth", 0);
            if (mat && mat.HasProperty("_BaseColor")) _seatedBlock.SetColor("_SColor", mat.GetColor("_BaseColor"));
            _seatedBlock.SetFloat("_IndirectIntensity", 0);
            _seatedBlock.SetFloat("_FresnelMin", 0);
            _seatedBlock.SetFloat("_FresnelMax", 0);
            r.SetPropertyBlock(_seatedBlock);
            root.gameObject.SetActive(true);
            return r;
        }

        public void ReleaseSeated(MeshRenderer r)
        {
            if (r == null) return;
            r.transform.parent.gameObject.SetActive(false);
            r.transform.parent.SetParent(_poolRoot, false);
            _seated.Push(r);
        }
    }
}
