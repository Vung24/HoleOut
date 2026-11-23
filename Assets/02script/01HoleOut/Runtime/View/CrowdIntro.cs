using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Thông số cảnh nhân vật chạy từ ngoài màn hình vào tranh khi bắt đầu màn.</summary>
    [Serializable]
    public class IntroSettings
    {
        [Range(0, 1), Tooltip("Cột có |vị trí chuẩn hoá| lớn hơn ngưỡng này vào từ mép trái/phải; phần giữa vào từ mép xa khay chờ.")]
        public float sideBand = 0.33f;
        [Range(0, 0.3f), Tooltip("Nhiễu ngưỡng chia vùng để ranh giới giữa các luồng không thẳng tắp.")]
        public float bandJitter = 0.12f;
        [Min(0), Tooltip("Điểm xuất phát nằm ngoài mép màn hình bao nhiêu ô.")]
        public float offscreenMargin = 1.5f;
        [Min(0), Tooltip("Độ toả của luồng (ô): lệch ngang và lệch sâu của điểm xuất phát để cả bầy chen thành cục.")]
        public float streamSpread = 1.5f;
        [Min(0), Tooltip("Độ trễ xuất phát ngẫu nhiên tối đa (giây).")]
        public float startDelayMax = 0.35f;
        [Min(0.05f), Tooltip("Thời gian chạy của con gần nhất (giây).")]
        public float runMin = 0.9f;
        [Min(0.05f), Tooltip("Thời gian chạy của con xa nhất (giây).")]
        public float runMax = 2.0f;
        [Min(0), Tooltip("Đứng chen tại ô trước khi ngồi (giây), ngẫu nhiên trong khoảng này.")]
        public Vector2 hold = new Vector2(0.05f, 0.25f);
        [Min(0.01f), Tooltip("Thời gian nhún xuống thành ô ngồi (giây).")]
        public float sitDuration = 0.2f;
        [Min(0), Tooltip("Biên độ đường chạy cong sang ngang (ô).")]
        public float sway = 0.6f;
        [Min(0), Tooltip("Độ nảy mỗi bước chạy (ô).")]
        public float hopHeight = 0.16f;
        [Min(0), Tooltip("Số bước chạy mỗi giây.")]
        public float stepRate = 4.5f;
    }

    /// <summary>
    /// Lịch chạy vào tranh của từng ô đang hiện. Mỗi ô chọn một cửa (trái, phải, mép xa khay chờ),
    /// xuất phát ngoài màn hình gần như cùng lúc; con gần tới trước nên tranh đầy dần từ mép vào trong.
    /// Toạ độ theo không gian cục bộ của gốc tranh. Chỉ tính tư thế, không giữ object.
    /// </summary>
    public sealed class CrowdIntro
    {
        public enum Phase { Hidden, Running, Holding, Sitting, Done }

        public struct Pose
        {
            public Phase Phase;
            public Vector3 Position;
            public Quaternion Rotation;
            /// <summary>Hệ số co giãn theo bước chạy/nhún.</summary>
            public Vector3 Squash;
            /// <summary>0 = dáng đứng chạy, 1 = ô ngồi; có thể vượt 1 một chút khi nảy.</summary>
            public float Seat;
        }

        struct Actor
        {
            public Vector3 From, To, Side;
            public float Delay, Run, Hold, Sway, Phase;
        }

        static readonly Quaternion FaceCamera = Quaternion.Euler(0, 180, 0);

        readonly IntroSettings _s;
        readonly float _cell;
        readonly Dictionary<int, Actor> _actors = new Dictionary<int, Actor>();

        public float Duration { get; private set; }

        public struct Cell
        {
            public int Index, Column;
            public Vector3 Target;
        }

        /// <param name="area">Vùng màn hình nhìn thấy trên mặt tranh (x, z) theo không gian cục bộ của gốc tranh.</param>
        public CrowdIntro(IntroSettings settings, IReadOnlyList<Cell> cells, int columns, Rect area, float cell, int seed)
        {
            _s = settings;
            _cell = cell;
            var rng = new System.Random(seed);
            float Rand() => (float)rng.NextDouble();
            float half = Mathf.Max(1, (columns - 1) / 2f), mid = (columns - 1) / 2f;
            float margin = _s.offscreenMargin * cell, spread = _s.streamSpread * cell;

            var built = new List<(int idx, Actor a, float dist)>(cells.Count);
            float maxDist = 0.001f;
            foreach (var c in cells)
            {
                float nx = (c.Column - mid) / half;
                float band = _s.sideBand + _s.bandJitter * (Rand() * 2 - 1);
                var to = c.Target;
                Vector3 from;
                if (nx < -band) from = new Vector3(area.xMin - margin - Rand() * spread, to.y, to.z + (Rand() * 2 - 1) * spread);
                else if (nx > band) from = new Vector3(area.xMax + margin + Rand() * spread, to.y, to.z + (Rand() * 2 - 1) * spread);
                else from = new Vector3(to.x + (Rand() * 2 - 1) * spread, to.y, area.yMax + margin + Rand() * spread);

                var dir = to - from;
                float dist = dir.magnitude;
                maxDist = Mathf.Max(maxDist, dist);
                var a = new Actor
                {
                    From = from,
                    To = to,
                    Side = Vector3.Cross(Vector3.up, dir).normalized,
                    Delay = Rand() * _s.startDelayMax,
                    Hold = Mathf.Lerp(_s.hold.x, _s.hold.y, Rand()),
                    Sway = (Rand() * 2 - 1) * _s.sway * cell,
                    Phase = Rand() * Mathf.PI,
                };
                built.Add((c.Index, a, dist));
            }

            Duration = 0;
            foreach (var (idx, a0, dist) in built)
            {
                var a = a0;
                a.Run = Mathf.Lerp(_s.runMin, _s.runMax, dist / maxDist);
                _actors[idx] = a;
                Duration = Mathf.Max(Duration, a.Delay + a.Run + a.Hold + _s.sitDuration);
            }
        }

        /// <summary>Tư thế của ô <paramref name="index"/> tại thời điểm <paramref name="t"/> kể từ lúc bắt đầu; ô không có lịch coi như đã ngồi.</summary>
        public Pose Sample(int index, float t)
        {
            var pose = new Pose { Phase = Phase.Done, Rotation = FaceCamera, Squash = Vector3.one, Seat = 1 };
            if (!_actors.TryGetValue(index, out var a)) return pose;
            pose.Position = a.To;
            float u = t - a.Delay;
            if (u < 0) { pose.Phase = Phase.Hidden; pose.Position = a.From; pose.Seat = 0; return pose; }

            if (u < a.Run)
            {
                float k = u / a.Run;
                float p = Progress(k);
                pose.Phase = Phase.Running;
                pose.Seat = 0;
                // Dập dềnh theo bước, tắt dần khi sắp tới ô.
                float step = Mathf.Abs(Mathf.Sin(a.Phase + u * _s.stepRate * Mathf.PI));
                float calm = 1 - Smooth((k - 0.8f) / 0.2f);
                pose.Position = PathAt(a, p) + Vector3.up * (step * _s.hopHeight * _cell * calm);
                var dir = PathAt(a, Mathf.Min(1, p + 0.02f)) - PathAt(a, Mathf.Max(0, p - 0.02f));
                dir.y = 0;
                var heading = dir.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(dir) : FaceCamera;
                pose.Rotation = heading * Quaternion.Euler(8 * calm, 0, 0);
                float stretch = 1 + 0.12f * step * calm;
                pose.Squash = new Vector3(1 / Mathf.Sqrt(stretch), stretch, 1 / Mathf.Sqrt(stretch));
                return pose;
            }

            u -= a.Run;
            var arrive = PathAt(a, 1) - PathAt(a, 0.97f);
            arrive.y = 0;
            var arriveRot = arrive.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(arrive) : FaceCamera;
            if (u < a.Hold)
            {
                // Đứng chen tại ô, quay dần về phía camera.
                float k = a.Hold > 0 ? u / a.Hold : 1;
                pose.Phase = Phase.Holding;
                pose.Seat = 0;
                pose.Rotation = Quaternion.Slerp(arriveRot, FaceCamera, Smooth(k));
                return pose;
            }

            u -= a.Hold;
            if (u < _s.sitDuration)
            {
                float k = u / _s.sitDuration;
                pose.Phase = Phase.Sitting;
                pose.Seat = OutBack(k);
                float press = Mathf.Sin(k * Mathf.PI);
                pose.Squash = new Vector3(1 + 0.08f * press, 1 - 0.2f * press, 1 + 0.08f * press);
                return pose;
            }
            return pose;
        }

        Vector3 PathAt(in Actor a, float p) =>
            Vector3.LerpUnclamped(a.From, a.To, p) + a.Side * (a.Sway * Mathf.Sin(p * Mathf.PI));

        /// <summary>Lao nhanh lúc đầu, chậm hẳn khi tới gần ô để cả bầy dồn lại chen chúc.</summary>
        static float Progress(float k)
        {
            float inv = 1 - k;
            return 0.35f * k + 0.65f * (1 - inv * inv * inv);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        static float OutBack(float k)
        {
            const float s = 1.70158f;
            k = Mathf.Clamp01(k) - 1;
            return k * k * ((s + 1) * k + s) + 1;
        }
    }
}
