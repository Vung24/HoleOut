using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Chụp riêng một nhóm renderer bằng camera chơi vào RenderTexture nền trong suốt, để HUD vẽ đè lên lớp tối
    /// đúng theo hình dạng vật thể. Layer chỉ đổi tạm trong lúc chụp rồi trả lại ngay; không gọi trong callback render.
    /// </summary>
    public sealed class FocusCapture : IDisposable
    {
        readonly Camera _camera;
        readonly int _layer;
        readonly List<GameObject> _objects = new List<GameObject>();
        readonly List<int> _layers = new List<int>();
        RenderTexture _texture;

        public FocusCapture(Camera camera, int layer)
        {
            _camera = camera;
            _layer = layer;
        }

        public Texture Render(List<Renderer> targets)
        {
            if (!_camera || _layer < 0 || targets.Count == 0) return null;
            int width = Mathf.Max(1, _camera.pixelWidth), height = Mathf.Max(1, _camera.pixelHeight);
            if (!_texture || _texture.width != width || _texture.height != height)
            {
                Release();
                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "TutorialFocus" };
                _texture.Create();
            }

            foreach (var r in targets)
            {
                if (!r) continue;
                _objects.Add(r.gameObject);
                _layers.Add(r.gameObject.layer);
                r.gameObject.layer = _layer;
            }

            int mask = _camera.cullingMask;
            var clear = _camera.clearFlags;
            var background = _camera.backgroundColor;
            var target = _camera.targetTexture;
            var active = RenderTexture.active;
            bool hdr = _camera.allowHDR, msaa = _camera.allowMSAA;
            try
            {
                _camera.cullingMask = 1 << _layer;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.clear;
                _camera.targetTexture = _texture;
                _camera.allowHDR = false;
                _camera.allowMSAA = false;
                _camera.Render();
            }
            finally
            {
                _camera.cullingMask = mask;
                _camera.clearFlags = clear;
                _camera.backgroundColor = background;
                _camera.targetTexture = target;
                _camera.allowHDR = hdr;
                _camera.allowMSAA = msaa;
                RenderTexture.active = active;
                for (int i = 0; i < _objects.Count; i++)
                    if (_objects[i]) _objects[i].layer = _layers[i];
                _objects.Clear();
                _layers.Clear();
            }
            return _texture;
        }

        public void Release()
        {
            if (!_texture) return;
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
            _texture = null;
        }

        public void Dispose() => Release();
    }
}
