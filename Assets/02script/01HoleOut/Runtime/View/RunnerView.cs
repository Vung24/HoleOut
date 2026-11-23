using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Diễn nhân vật rời tranh theo các pha mô phỏng đã lên lịch (độ dài pha nằm ở <see cref="MotionConfig"/>):
    /// giật mình → chạy (có thể vấp lộn một vòng) → phanh trượt ở mép → (con cuối) ngó xuống, khựng một nhịp → nhún → rơi vào hố.
    /// Chỉ đổi vị trí/góc/tỉ lệ gốc nhân vật; thời điểm do mô phỏng quyết định.
    /// </summary>
    public sealed class RunnerView
    {
        // Cú rơi vào hố: clip nhảy rời đất ở giây 1/3 (sự kiện OnJumpStart), bay parabol rồi chui xuống điểm đáp trong lòng hố.
        const float JumpTakeoffTime = 1f / 3f;
        const float JumpArcHeight = 1.2f;
        const float HoleLandingDepth = 0.61f;
        // Điểm nhấn 2: con cuối trượt chân ngã ngửa xuống hố.
        const float SlipSlide = 0.12f;     // chân vọt ra trước qua mép (nhân tỉ lệ nhân vật)
        const float SlipHop = 0.1f;        // bị hất lên khi hẫng chân
        const float SlipLean = -70f;       // góc ngửa người lúc rời mép
        const float SlipArcHeight = 0.15f; // nhịp lơ lửng đầu cú rơi
        // Vấp ngã ở 45% quãng chạy.
        const float TripAt = 0.45f;
        // Quãng trôi tới trước trong lúc ngã và lăn, tính theo chiều cao thân.
        const float TripRollLength = 1.3f;
        const float StartledHop = 0.09f;
        // Booster nam châm: độ cao vòng bay khi hút nhân vật.
        const float MagnetArcHeight = 1.1f;

        readonly GameSimulation _sim;
        readonly CharacterPool _pool;
        readonly PictureView _picture;
        readonly HoleView _holes;
        readonly Transform _parent;
        readonly float _agentScale;
        readonly Dictionary<int, AgentRig> _runners = new Dictionary<int, AgentRig>();

        public RunnerView(GameSimulation sim, CharacterPool pool, PictureView picture,
            HoleView holes, Transform parent, float agentScale)
        {
            _sim = sim;
            _pool = pool;
            _picture = picture;
            _holes = holes;
            _parent = parent;
            _agentScale = agentScale;
        }

        public void Clear()
        {
            foreach (var rig in _runners.Values) _pool.ReleaseAgent(rig);
            _runners.Clear();
        }

        public void OnDispatched(int runnerId)
        {
            Runner r = null;
            foreach (var x in _sim.Runners) if (x.Id == runnerId) { r = x; break; }
            if (r == null) return;
            int w = _sim.Picture.Rows - 1 - r.SourceRow;
            int idx = _sim.Picture.Index(r.Column, w);
            if (!_picture.TakeStanding(idx, out var rig)) rig = _pool.AcquireAgent(r.Color, _picture.Content, _agentScale);
            // Đặt về ô xuất phát: nhân vật lấy từ pool còn giữ vị trí lần nhảy trước.
            rig.transform.SetParent(_parent, true);
            rig.transform.SetPositionAndRotation(BoardSpace.ToWorld(r.Route[0]), BoardSpace.FaceCamera);
            rig.transform.localScale = Vector3.one * _agentScale;
            _runners[runnerId] = rig;
        }

        public void OnLanded(int runnerId)
        {
            if (!_runners.TryGetValue(runnerId, out var rig)) return;
            _pool.ReleaseAgent(rig);
            _runners.Remove(runnerId);
        }

        public void Render()
        {
            var m = _sim.Motion;
            var faceCamera = BoardSpace.FaceCamera;
            float ready = (float)m.Ready, brake = (float)m.Brake, crouch = (float)m.Crouch;
            float s = _agentScale;
            foreach (var r in _sim.Runners)
            {
                if (!_runners.TryGetValue(r.Id, out var rig)) continue;
                var tr = rig.transform;
                float t = (float)(r.Elapsed - r.StartDelay);
                var start = BoardSpace.ToWorld(r.Route[0]);
                if (t < 0)
                {
                    // Chờ làn đáp tại ô xuất phát.
                    tr.SetPositionAndRotation(start, faceCamera);
                    tr.localScale = Vector3.one * s;
                    continue;
                }
                float pre = (float)r.PreAir, air = (float)r.AirDuration;
                float trip = r.Tripped ? (float)m.Trip : 0, tease = r.Finale ? (float)m.Tease : 0;
                float run = Mathf.Max(0.01f, pre - ready - trip - brake - tease - crouch);
                float runEnd = ready + run + trip, brakeEnd = runEnd + brake, teaseEnd = brakeEnd + tease;
                var end = BoardSpace.ToWorld(r.Route[r.Route.Count - 1]);
                var face = BoardSpace.ToWorld(r.Target) - end;
                face.y = 0;
                var faceRot = face.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(face) : faceCamera;
                // Đường chạy vượt mép một đoạn để trượt phanh rồi lùi về mép.
                var ledge = end - (face.sqrMagnitude > 1e-6f ? face.normalized : Vector3.zero) * (float)MotionConfig.RouteOvershoot;

                Vector3 pos;
                Quaternion rot;
                var squash = Vector3.one;
                if (t < ready)
                {
                    rig.Run();
                    var heading = RouteHeading(r, 0, faceRot);
                    if (t < StartledHop)
                    {
                        // Giật mình: bật nhẹ lên, quay về hướng chạy.
                        float k = t / StartledHop, pop = Mathf.Sin(k * Mathf.PI);
                        pos = start + Vector3.up * (0.1f * pop * s);
                        rot = Quaternion.Slerp(faceCamera, heading, Smooth(k));
                        squash = new Vector3(1 - 0.08f * pop, 1 + 0.18f * pop, 1 - 0.08f * pop);
                    }
                    else
                    {
                        // Đạp chân tại chỗ, người chúi về trước lấy đà.
                        float k = (t - StartledHop) / (ready - StartledHop), spin = t * 55;
                        pos = start + Vector3.up * (Mathf.Abs(Mathf.Sin(spin)) * 0.03f * s);
                        rot = heading * Quaternion.Euler(0.45f * Smooth(k * 2) * Mathf.Rad2Deg, 0, Mathf.Sin(spin * 0.5f) * 0.05f * Mathf.Rad2Deg);
                    }
                }
                else if (t < runEnd)
                {
                    float rt = t - ready, tripK = -1;
                    float ts = TripAt * run;
                    double dist = r.RunDistance, d;
                    if (trip > 0 && rt >= ts)
                    {
                        // Quán tính: trong lúc ngã và lăn vẫn trôi tới trước khoảng một thân người,
                        // phần đường còn lại dồn vào quãng chạy sau khi bật dậy nên vẫn tới mép đúng lịch.
                        float easeTs = RunEase(TripAt);
                        double dTs = dist * easeTs;
                        double roll = System.Math.Min(TripRollLength * rig.bodyHeight * s, (dist - dTs) * 0.6);
                        if (rt < ts + trip)
                        {
                            tripK = (rt - ts) / trip;
                            d = dTs + roll * (1 - (1 - tripK) * (1 - tripK));
                        }
                        else
                        {
                            float e = RunEase(Mathf.Clamp01((rt - trip) / run));
                            d = dTs + roll + (dist - dTs - roll) * (e - easeTs) / Mathf.Max(1e-4f, 1 - easeTs);
                        }
                    }
                    else d = dist * RunEase(Mathf.Clamp01(rt / run));
                    var q = RouteAt(r, d);
                    var heading = RouteHeading(r, d, faceRot);
                    rig.Run();
                    if (tripK >= 0)
                    {
                        // Vấp: chúi ngã quanh bàn chân → cuộn tròn lăn sát sàn → bật dậy hơi ngả ra sau rồi chạy tiếp.
                        float x = tripK;
                        rig.Stumble(x < 0.8f);
                        float pitch = 70 * Smooth(x / 0.22f) + 290 * Smooth((x - 0.18f) / 0.6f)
                                      - 12 * Mathf.Sin(Smooth((x - 0.78f) / 0.22f) * Mathf.PI);
                        float ball = Smooth((x - 0.2f) / 0.1f) * (1 - Smooth((x - 0.72f) / 0.1f));
                        // Trục xoay chuyển dần từ bàn chân lên tâm quả bóng (chạm sàn) rồi về lại bàn chân.
                        float pivot = 0.36f * rig.bodyHeight * s * Smooth((x - 0.12f) / 0.18f) * (1 - Smooth((x - 0.72f) / 0.14f));
                        float hop = Mathf.Sin(Smooth((x - 0.8f) / 0.2f) * Mathf.PI) * 0.06f * s;
                        rot = heading * Quaternion.Euler(pitch, 0, 0);
                        pos = q + Vector3.up * (pivot + hop) - rot * Vector3.up * pivot;
                        squash = new Vector3(1 + 0.12f * ball, 1 - 0.3f * ball, 1 + 0.12f * ball);
                        rig.AnchorShadow(q);
                    }
                    else
                    {
                        rig.Stumble(false);
                        pos = q;
                        // Nam châm hút: bay vòng qua đầu các nhân vật khác.
                        if (r.Lifted && dist > 1e-6) pos += Vector3.up * (MagnetArcHeight * Mathf.Sin((float)(d / dist) * Mathf.PI));
                        rot = heading * Quaternion.Euler(8, 0, 0);
                    }
                }
                else if (t < brakeEnd)
                {
                    // Trượt phanh ở mép, ngả người ra sau chới với rồi đứng vững. Con cuối vẫn đạp chân để nối liền sang nhịp chới với.
                    if (r.Finale) rig.Scramble(); else rig.Stop();
                    float k = (t - runEnd) / brake, wob = Mathf.Sin(k * Mathf.PI);
                    pos = Vector3.Lerp(end, ledge, Smooth(k));
                    float pitch = 0.3f * (1 - k) - 0.45f * wob + Mathf.Sin(k * Mathf.PI * 3) * 0.08f * wob;
                    rot = faceRot * Quaternion.Euler(pitch * Mathf.Rad2Deg, 0, 0);
                    squash = new Vector3(1 + 0.08f * wob, 1 - 0.08f * wob, 1 + 0.08f * wob);
                }
                else if (t < teaseEnd)
                {
                    // Con cuối khựng một nhịp: chúi qua mép chới với, chân cuống quýt đạp, giật người lại rồi nhảy.
                    rig.Scramble();
                    float k = (t - brakeEnd) / tease;
                    float lean = Smooth(k / 0.25f) * (1 - Smooth((k - 0.65f) / 0.3f));
                    float wobble = Mathf.Sin(k * Mathf.PI * 6) * lean;
                    float pitch = 30 * lean + 7 * wobble;
                    float tilt = 10 * Mathf.Sin(k * Mathf.PI * 4) * lean;
                    pos = ledge + faceRot * Vector3.forward * (0.05f * s * lean);
                    rot = faceRot * Quaternion.Euler(pitch, 0, tilt);
                    float pull = Mathf.Sin(Smooth((k - 0.65f) / 0.35f) * Mathf.PI);
                    squash = new Vector3(1 - 0.05f * pull, 1 + 0.1f * pull, 1 - 0.05f * pull);
                }
                else if (t < pre && r.Finale)
                {
                    // Con cuối không nhún: chân hẫng vọt ra trước qua mép, người bật ngửa ra sau, hơi bị hất lên.
                    rig.Scramble();
                    float k = Mathf.Clamp01((t - teaseEnd) / crouch);
                    float f = 1 - (1 - k) * (1 - k) * (1 - k);
                    rot = faceRot * Quaternion.Euler(SlipLean * f, 0, 0);
                    float mid = rig.bodyHeight * 0.5f * s;
                    pos = ledge + faceRot * Vector3.forward * (SlipSlide * s * f)
                          + Vector3.up * (mid + SlipHop * s * f) - rot * Vector3.up * mid;
                    float jolt = Mathf.Sin(k * Mathf.PI);
                    squash = new Vector3(1 - 0.06f * jolt, 1 + 0.12f * jolt, 1 - 0.06f * jolt);
                    rig.AnchorShadow(ledge);
                }
                else if (t < pre)
                {
                    // Nhún lấy đà; clip nhảy chạy sao cho lúc rời đất trùng lúc bắt đầu bay.
                    rig.Jump(JumpTakeoffTime / Mathf.Max(0.01f, crouch));
                    float press = Mathf.Sin((t - teaseEnd) / crouch * Mathf.PI * 0.5f);
                    pos = ledge;
                    rot = faceRot * Quaternion.Euler(0.2f * press * Mathf.Rad2Deg, 0, 0);
                    squash = new Vector3(1 + 0.12f * press, 1 - 0.26f * press, 1 + 0.12f * press);
                }
                else
                {
                    // Rơi vào hố: parabol từ mép tới điểm đáp nằm sâu trong lòng hố, lớp trong hố che dần khi chui xuống dưới mặt khay.
                    rig.Launch();
                    float k = Mathf.Clamp01((t - pre) / air);
                    float floorY = _holes.TryGet(r.HoleId, out var hole) ? hole.transform.position.y : 0;
                    // Đáp vào tâm hố để thân không quệt lên thành.
                    var landing = BoardSpace.ToWorld(r.Target, floorY - HoleLandingDepth * _holes.Scale);
                    // Điểm nhấn duy nhất: con cuối trượt chân ngã ngửa; các con khác nhảy gọn vào giữa hố.
                    if (r.Finale)
                    {
                        // Trượt chân: lơ lửng một nhịp chới với rồi rơi tuột, mông xuống trước, chân vẫn đạp loạn xạ.
                        rig.Scramble();
                        var from = ledge + faceRot * Vector3.forward * (SlipSlide * s) + Vector3.up * (SlipHop * s);
                        // Ngang tới tâm hố sớm (ease-out) trong khi rơi đứng theo k², nên đã ở trên giữa miệng hố trước khi tụt xuống ngang thành.
                        float h = 1 - (1 - k) * (1 - k);
                        var flat = Vector3.Lerp(from, landing, h);
                        // Rơi theo k² nên đầu cú rơi gần như đứng yên (khựng giữa không), về cuối tăng tốc.
                        flat.y = Mathf.Lerp(from.y, landing.y, k * k) + 4 * SlipArcHeight * k * (1 - k) * (1 - k);
                        float flail = Mathf.Sin(k * Mathf.PI * 5) * (1 - k);
                        // Gập về dáng ngồi khi rơi: thân gọn lại, không chìa đầu ra phía thành.
                        rot = faceRot * Quaternion.Euler(SlipLean + 40 * Smooth(k) + 6 * flail, 0, 10 * flail);
                        float mid = rig.bodyHeight * 0.5f * s;
                        pos = flat + Vector3.up * mid - rot * Vector3.up * mid;
                    }
                    else
                    {
                        rig.Jump(JumpTakeoffTime / Mathf.Max(0.01f, crouch));
                        pos = Vector3.Lerp(ledge, landing, k) + Vector3.up * (4 * JumpArcHeight * k * (1 - k));
                        rot = faceRot;
                        squash = Stretch(k);
                    }
                }
                tr.SetPositionAndRotation(pos, rot);
                tr.localScale = squash * s;
            }
        }

        static float Smooth(float x) => BoardSpace.Smooth(x);

        /// <summary>Vươn dài người lúc bật lên rồi trở lại dáng thường khi đang bay.</summary>
        static Vector3 Stretch(float k)
        {
            float stretch = 1 + 0.22f * Mathf.Sin(Mathf.Min(1, k * 4) * Mathf.PI);
            float thin = 1 / Mathf.Sqrt(stretch);
            return new Vector3(thin, stretch, thin);
        }

        /// <summary>Lao đi nhanh, chậm dần khi tới mép.</summary>
        static float RunEase(float k) => k * 0.55f + (1 - (1 - k) * (1 - k)) * 0.45f;

        /// <summary>Hướng nhìn hơi về phía trước trên đường chạy để cua mượt.</summary>
        static Quaternion RouteHeading(Runner r, double d, Quaternion fallback)
        {
            var dir = RouteAt(r, System.Math.Min(r.RunDistance, d + 0.3)) - RouteAt(r, d);
            dir.y = 0;
            return dir.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(dir) : fallback;
        }

        /// <summary>Điểm trên đường chạy sau quãng <paramref name="target"/> tính từ ô xuất phát.</summary>
        static Vector3 RouteAt(Runner r, double target)
        {
            double acc = 0;
            for (int i = 1; i < r.Route.Count; i++)
            {
                double seg = Vec2.Distance(r.Route[i - 1], r.Route[i]);
                if (acc + seg >= target && seg > 1e-6)
                    return Vector3.Lerp(BoardSpace.ToWorld(r.Route[i - 1]), BoardSpace.ToWorld(r.Route[i]), (float)((target - acc) / seg));
                acc += seg;
            }
            return BoardSpace.ToWorld(r.Route[r.Route.Count - 1]);
        }
    }
}
