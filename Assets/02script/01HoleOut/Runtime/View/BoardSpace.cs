using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Đổi tọa độ giữa mô phỏng và Unity. Tọa độ mô phỏng (x, z) đổi sang Unity là (x, y, -z)
    /// để tranh nằm phía xa camera; bàn khối dùng lưới ô riêng với gốc ở góc trên trái.
    /// </summary>
    public static class BoardSpace
    {
        public const float BoardCell = 1.24f;
        public const float BoardCenterZ = 4.25f;
        public static readonly float BoardStartX = -3 * BoardCell;
        public static readonly float BoardStartZ = BoardCenterZ - 2 * BoardCell;
        /// <summary>Hướng nhân vật/ô tranh nhìn về camera.</summary>
        public static readonly Quaternion FaceCamera = Quaternion.Euler(0, 180, 0);

        public static Vector3 ToWorld(Vec2 p, float y = 0) => new Vector3((float)p.X, y, -(float)p.Z);

        /// <summary>Bàn khối dựng nghiêng về phía camera: đổi từ mặt bàn phẳng sang thế giới.</summary>
        static Matrix4x4 _lean = Matrix4x4.identity, _leanInverse = Matrix4x4.identity;
        public static Quaternion LeanRotation { get; private set; } = Quaternion.identity;

        /// <summary>
        /// Xoay bàn khối quanh mép trên (phía khay) để mặt bàn hướng về camera, rồi đẩy theo hướng nhìn
        /// để mép dưới không chìm dưới nền; camera trực giao nên vị trí trên màn hình không đổi.
        /// </summary>
        public static void SetLean(float angle, float cameraPitch)
        {
            LeanRotation = Quaternion.Euler(-angle, 0, 0);
            var pivot = new Vector3(0, 0, -BoardStartZ);
            float depth = (DragSolver.BoardHeight + 1) * BoardCell;
            var back = Quaternion.Euler(cameraPitch, 0, 0) * Vector3.back;
            var shift = back.y > 0.01f ? back * (depth * Mathf.Sin(angle * Mathf.Deg2Rad) / back.y) : Vector3.zero;
            _lean = Matrix4x4.Translate(pivot + shift) * Matrix4x4.Rotate(LeanRotation) * Matrix4x4.Translate(-pivot);
            _leanInverse = _lean.inverse;
        }

        public static Vector3 BoardToWorld(double vx, double vy, int w, int h, float y = 0) =>
            _lean.MultiplyPoint3x4(new Vector3(BoardStartX + (float)(vx + w / 2.0) * BoardCell, y, -(BoardStartZ + (float)(vy + h / 2.0) * BoardCell)));

        public static Vector2 WorldToBoard(Vector3 world)
        {
            var flat = _leanInverse.MultiplyPoint3x4(world);
            return new Vector2((flat.x - BoardStartX) / BoardCell, (-flat.z - BoardStartZ) / BoardCell);
        }

        /// <summary>Mặt phẳng song song mặt bàn ở độ cao <paramref name="y"/> so với bàn.</summary>
        public static Plane BoardPlane(float y) =>
            new Plane(LeanRotation * Vector3.up, _lean.MultiplyPoint3x4(new Vector3(0, y, 0)));

        /// <summary>Đặt object dựng sẵn theo mặt bàn phẳng sang vị trí bàn đã dựng nghiêng.</summary>
        public static void ApplyLean(Transform t)
        {
            t.SetPositionAndRotation(_lean.MultiplyPoint3x4(t.position), LeanRotation * t.rotation);
        }

        public static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }
    }
}
