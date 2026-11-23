using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Hiệu ứng của hố trên khay: chấm sáng khi nhân vật lọt hố và hiệu ứng khi hố đầy.
    /// Chỉ trình diễn; không đổi trạng thái hố, nên tắt hay bỏ lớp này không ảnh hưởng số đếm, đóng hố và trả chỗ.
    /// Pool hiệu ứng giữ qua các màn.
    /// </summary>
    public sealed class HoleEffects
    {
        const float FxLifetime = 2.5f;

        readonly DockVisuals _visuals;
        readonly Transform _poolRoot;
        readonly List<GameObject> _fxPool = new List<GameObject>();
        readonly List<float> _fxUntil = new List<float>();
        readonly List<ParticleSystem> _fxSystems = new List<ParticleSystem>();
        readonly Dictionary<ParticleSystem, Color> _fxBaseColors = new Dictionary<ParticleSystem, Color>();
        ParticleSystem _catchSparks;

        public HoleEffects(DockVisuals visuals, Transform poolRoot)
        {
            _visuals = visuals;
            _poolRoot = poolRoot;
        }

        public void Clear()
        {
            for (int i = 0; i < _fxPool.Count; i++) { if (_fxPool[i]) _fxPool[i].SetActive(false); _fxUntil[i] = 0; }
        }

        /// <summary>Nhân vật lọt hố: tung vài chấm tròn màu hố từ miệng hố. Cú nảy của hố do rig hố lo.</summary>
        public void PlayCatch(HoleRig rig, string color, float holeScale)
        {
            int count = _visuals.CatchSparks;
            if (count <= 0 || !rig) return;
            var ps = CatchSparks();
            if (!ps) return;
            var tint = _visuals.Palette.ColorOf(color);
            float mouth = rig.MouthRadius > 0 ? rig.MouthRadius : 0.3f * holeScale;
            var center = rig.transform.position + Vector3.up * 0.1f;
            float speed = _visuals.CatchSparkSpeed, size = _visuals.CatchSparkSize * holeScale;
            var p = new ParticleSystem.EmitParams();
            float phase = Random.value * Mathf.PI * 2;
            for (int i = 0; i < count; i++)
            {
                // Tỏa đều quanh miệng hố, hơi lệch ngẫu nhiên để mỗi lần trông khác nhau.
                float a = phase + i * Mathf.PI * 2 / count + Random.Range(-0.25f, 0.25f);
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                p.position = center + dir * (mouth * 0.6f);
                p.velocity = dir * (speed * Random.Range(0.35f, 0.6f)) + Vector3.up * (speed * Random.Range(0.8f, 1.2f));
                p.startSize = size * Random.Range(0.85f, 1.15f);
                p.startColor = tint;
                p.startLifetime = Random.Range(0.35f, 0.5f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>Hố đầy: tái dùng instance đã chạy xong, bật lại để particle phát từ đầu.</summary>
        public void PlayComplete(HoleRig rig, string color)
        {
            if (!_visuals.HoleCompleteEffect || !rig) return;
            var pos = rig.transform.position + Vector3.up * 0.2f;
            float now = Time.time;
            int slot = -1;
            for (int i = 0; i < _fxPool.Count; i++)
                if (_fxPool[i] && now >= _fxUntil[i]) { slot = i; break; }
            GameObject fx;
            if (slot < 0)
            {
                fx = Object.Instantiate(_visuals.HoleCompleteEffect, _poolRoot);
                _fxPool.Add(fx);
                _fxUntil.Add(0);
                slot = _fxPool.Count - 1;
            }
            else fx = _fxPool[slot];
            fx.SetActive(false);
            Tint(fx, color != null ? HighlightColor(color) : Color.white);
            fx.transform.SetPositionAndRotation(pos, Quaternion.identity);
            fx.SetActive(true);
            _fxUntil[slot] = now + FxLifetime;
        }

        /// <summary>Một hệ hạt billboard dùng chung cho mọi hố, phát bằng Emit; màu lấy theo hố qua startColor.</summary>
        ParticleSystem CatchSparks()
        {
            if (_catchSparks) return _catchSparks;
            if (!_visuals.CatchSparkMaterial)
            {
                Debug.LogWarning("HoleOut: chưa gán material hạt cho hiệu ứng nhân vật lọt hố (HoleOutVisuals).");
                return null;
            }
            var go = new GameObject("HoleCatchSparks");
            go.transform.SetParent(_poolRoot, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            // Chạy liên tục (không tự phát) để hạt Emit lúc nào cũng được mô phỏng.
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.9f;
            main.maxParticles = 256;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                new Keyframe(0, 0.6f), new Keyframe(0.2f, 1), new Keyframe(1, 0)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = _visuals.CatchSparkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return _catchSparks = ps;
        }

        /// <summary>Nhuộm hạt hiệu ứng theo màu hố, giữ độ trong suốt gốc của từng hệ hạt.</summary>
        void Tint(GameObject fx, Color tint)
        {
            fx.GetComponentsInChildren(true, _fxSystems);
            foreach (var ps in _fxSystems)
            {
                var main = ps.main;
                if (!_fxBaseColors.TryGetValue(ps, out var baseColor))
                    _fxBaseColors[ps] = baseColor = main.startColor.color;
                var c = tint;
                c.a = baseColor.a;
                main.startColor = c;
            }
        }

        /// <summary>Màu vạch sáng của màu chuẩn, dùng nhuộm hạt hiệu ứng hố đầy.</summary>
        Color HighlightColor(string code) =>
            PaletteSO.TryGetSwatch(code, out var swatch)
                ? PaletteSO.FromHex(swatch.highlight)
                : _visuals.Palette.ColorOf(code);
    }
}
