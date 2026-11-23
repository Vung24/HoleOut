using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Căn camera trực giao nghiêng cho khung nội dung thật vừa khít vùng màn hình dưới HUD;
    /// căn lại khi đổi cỡ màn hình.
    /// </summary>
    public sealed class CameraFitter
    {
        /// <summary>Khoảng chừa quanh nội dung, tính theo phần màn hình.</summary>
        const float EdgeMargin = 0.015f;

        readonly Camera _camera;
        readonly GameHud _hud;
        Vector2Int _fittedScreen;
        readonly System.Collections.Generic.List<Vector3> _corners = new System.Collections.Generic.List<Vector3>();

        public CameraFitter(Camera camera, GameHud hud)
        {
            _camera = camera;
            _hud = hud;
        }

        public bool ScreenChanged => Screen.width != _fittedScreen.x || Screen.height != _fittedScreen.y;

        /// <summary>Chiếu 8 góc khung lên mặt phẳng camera, rồi chọn cỡ ortho và vị trí theo vùng trống.</summary>
        public void Fit(GameView view, float pitch)
        {
            if (_camera == null || view == null) return;
            _fittedScreen = new Vector2Int(Screen.width, Screen.height);
            _camera.orthographic = true;
            var rot = Quaternion.Euler(pitch, 0, 0);
            var inv = Quaternion.Inverse(rot);
            var b = view.ContentWorldBounds();
            view.BoardCorners(_corners);

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 8; i++)
                _corners.Add(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
            foreach (var corner in _corners)
            {
                var local = inv * corner;
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y); maxY = Mathf.Max(maxY, local.y);
            }

            // Vùng trống theo phần chiều cao màn hình: từ đáy tới dưới thanh HUD.
            float top = 1 - (_hud != null ? _hud.TopBarFraction() : 0.18f) - EdgeMargin;
            float bottom = (_hud != null ? _hud.BottomBarFraction() : 0.14f) + EdgeMargin;
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float sizeForHeight = (maxY - minY) / (2 * (top - bottom));
            float sizeForWidth = (maxX - minX) / (2 * aspect * (1 - 2 * EdgeMargin));
            float size = Mathf.Max(sizeForHeight, sizeForWidth);
            _camera.orthographicSize = size;

            // Căn mép trên của nội dung ngay dưới HUD.
            float cx = (minX + maxX) / 2;
            float cy = maxY - (top - 0.5f) * 2 * size;
            var camLocal = new Vector3(cx, cy, -40);
            _camera.transform.SetPositionAndRotation(rot * camLocal, rot);
            view.FitGround(_camera);
        }
    }
}
