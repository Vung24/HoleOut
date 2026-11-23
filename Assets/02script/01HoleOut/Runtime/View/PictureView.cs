using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Trình diễn tranh của một màn: ô ngồi, nhân vật đứng dậy chờ hố, cuộn tranh và cảnh chạy vào lúc mở màn.
    /// Chỉ đọc <see cref="Picture"/>; nhân vật rời tranh được giao cho <see cref="RunnerView"/>.
    /// </summary>
    public sealed class PictureView
    {
        readonly GameSimulation _sim;
        readonly PictureVisuals _visuals;
        readonly CharacterPool _pool;
        readonly Transform _content;
        readonly float _cell;
        readonly float _agentScale;
        readonly Vector3 _seatedScale;

        readonly Dictionary<int, MeshRenderer> _seated = new Dictionary<int, MeshRenderer>();
        readonly Dictionary<int, AgentRig> _standing = new Dictionary<int, AgentRig>();
        readonly HashSet<int> _standingAlive = new HashSet<int>();
        readonly HashSet<int> _seatedAlive = new HashSet<int>();
        readonly List<int> _dead = new List<int>();

        // Intro: nhân vật chạy từ ngoài màn hình vào ô của mình (lịch trong CrowdIntro).
        CrowdIntro _intro;
        float _introStart = -1;
        bool _introDone;
        readonly List<CrowdIntro.Cell> _introCells = new List<CrowdIntro.Cell>();
        // Nhịp lắng khi tranh dừng cuộn: chỉ là độ lệch hiển thị, không đổi vị trí gameplay.
        // Vùng nhìn thấy trên mặt đất (x, z thế giới), cập nhật khi camera căn lại.
        Rect _viewArea;
        bool _hasViewArea;

        /// <summary>Gốc nội dung tranh, cuộn theo tranh; khay tranh nằm ngoài gốc này nên đứng yên.</summary>
        public Transform Content => _content;
        public bool IntroPlaying => !_introDone;

        public PictureView(GameSimulation sim, PictureVisuals visuals, CharacterPool pool,
            Transform content, float cell, float agentScale, Vector3 seatedScale, bool playIntro)
        {
            _sim = sim;
            _visuals = visuals;
            _pool = pool;
            _content = content;
            _cell = cell;
            _agentScale = agentScale;
            _seatedScale = seatedScale;
            _introDone = !playIntro;
        }

        public void SetViewArea(Rect area)
        {
            _viewArea = area;
            _hasViewArea = true;
        }

        /// <summary>Lấy nhân vật đang đứng ở ô <paramref name="index"/> (nếu có) để chuyển sang đường chạy.</summary>
        public bool TakeStanding(int index, out AgentRig rig)
        {
            if (!_standing.TryGetValue(index, out rig)) return false;
            _standing.Remove(index);
            return true;
        }

        public void Clear()
        {
            foreach (var rig in _standing.Values) _pool.ReleaseAgent(rig);
            foreach (var r in _seated.Values) _pool.ReleaseSeated(r);
            _standing.Clear();
            _seated.Clear();
        }

        public void Render(float time)
        {
            var p = _sim.Picture;
            int vis = p.VisibleRows;
            var faceCamera = BoardSpace.FaceCamera;
            // Ô ngồi ngả về phía camera để lộ mặt trước khối, bàn kéo khối vẫn giữ góc nhìn dốc.
            float tilt = _visuals.CellTilt;
            var seatedRotation = Quaternion.Euler(tilt, 0, 0) * faceCamera;
            // Cuộn toàn bộ tranh bằng root riêng; khay luôn đứng yên.
            _content.localPosition = Vector3.back * (float)(p.ScrollVisual * _cell);
            if (!_introDone && _intro == null) { _intro = BuildIntro(); _introStart = time; }
            float introT = time - _introStart;
            if (!_introDone && introT >= _intro.Duration) { _introDone = true; _intro = null; }
            bool intro = !_introDone;
            _standingAlive.Clear();
            _seatedAlive.Clear();

            for (int w = p.WindowLow; w < p.Rows && w <= p.WindowHigh + 1; w++)
            {
                double visualRow = (vis - 1) - w + p.ScrollVisual;
                if (visualRow < -0.99 || visualRow > vis - 1 + 0.01) continue;
                for (int c = 0; c < p.Columns; c++)
                {
                    if (!p.Occupied(c, w)) continue;
                    var pos = BoardSpace.ToWorld(_sim.Layout.CellToWorld(c, (vis - 1) - w));
                    int idx = p.Index(c, w);
                    float stand = (float)p.Stand(c, w);
                    if (stand <= 0.01f)
                    {
                        // Ngồi: khối instancing; trả nhân vật về pool nếu vừa ngồi xuống.
                        if (_standing.TryGetValue(idx, out var old)) { _pool.ReleaseAgent(old); _standing.Remove(idx); }
                        if (!_seated.TryGetValue(idx, out var seat))
                            _seated[idx] = seat = _pool.AcquireSeated(p.ColorAt(c, w), _content, _seatedScale);
                        var seatRoot = seat.transform.parent;
                        if (intro)
                        {
                            // Ô ngồi đóng vai nhân vật chạy (nhẹ hơn nhân vật có xương khi cả tranh cùng chạy vào).
                            var ip = _intro.Sample(idx, introT);
                            bool show = ip.Phase != CrowdIntro.Phase.Hidden;
                            if (seatRoot.gameObject.activeSelf != show) seatRoot.gameObject.SetActive(show);
                            seatRoot.localPosition = ip.Position;
                            seatRoot.localRotation = Quaternion.Euler(tilt * ip.Seat, 0, 0) * ip.Rotation;
                            var size = Vector3.LerpUnclamped(Vector3.one * _agentScale, _seatedScale, ip.Seat);
                            seatRoot.localScale = Vector3.Scale(size, ip.Squash);
                        }
                        else
                        {
                            if (!seatRoot.gameObject.activeSelf) seatRoot.gameObject.SetActive(true);
                            seatRoot.localPosition = pos;
                            seatRoot.localRotation = seatedRotation;
                            seatRoot.localScale = _seatedScale;
                        }
                        _seatedAlive.Add(idx);
                        continue;
                    }
                    _standingAlive.Add(idx);
                    if (!_standing.TryGetValue(idx, out var rig))
                        _standing[idx] = rig = _pool.AcquireAgent(p.ColorAt(c, w), _content, _agentScale);
                    if (intro)
                    {
                        // Ô đứng sẵn: chạy vào như mọi ô nhưng giữ dáng đứng khi tới nơi.
                        var ip = _intro.Sample(idx, introT);
                        bool show = ip.Phase != CrowdIntro.Phase.Hidden;
                        if (rig.gameObject.activeSelf != show) rig.gameObject.SetActive(show);
                        if (ip.Phase == CrowdIntro.Phase.Running) rig.Run(); else rig.Stop();
                        rig.transform.localPosition = ip.Position;
                        rig.transform.localRotation = ip.Rotation;
                        rig.transform.localScale = ip.Phase == CrowdIntro.Phase.Running ? ip.Squash * _agentScale : Vector3.one * _agentScale;
                        continue;
                    }
                    if (!rig.gameObject.activeSelf) rig.gameObject.SetActive(true);
                    float bob = p.ReadyHole(c, w) >= 0 ? Mathf.Abs(Mathf.Sin(time * 6.5f + c * 1.3f + w)) * 0.05f * stand : 0;
                    rig.transform.localPosition = pos + Vector3.up * bob;
                    rig.transform.localRotation = faceCamera;
                    rig.transform.localScale = Vector3.one * _agentScale;
                }
            }

            // Nhân vật đứng nhưng không còn trong cửa sổ (đã rời tranh hoặc tranh vừa trượt).
            _dead.Clear();
            foreach (var kv in _standing) if (!_standingAlive.Contains(kv.Key)) _dead.Add(kv.Key);
            foreach (var idx in _dead) { _pool.ReleaseAgent(_standing[idx]); _standing.Remove(idx); }
            _dead.Clear();

            foreach (var kv in _seated) if (!_seatedAlive.Contains(kv.Key)) _dead.Add(kv.Key);
            foreach (var idx in _dead) if (_seated.TryGetValue(idx, out var seat)) { _pool.ReleaseSeated(seat); _seated.Remove(idx); }
        }


        /// <summary>Lập lịch chạy vào cho mọi ô đang hiện; vùng màn hình đổi sang không gian cục bộ của gốc tranh.</summary>
        CrowdIntro BuildIntro()
        {
            var p = _sim.Picture;
            int vis = p.VisibleRows;
            _introCells.Clear();
            for (int w = p.WindowLow; w <= p.WindowHigh && w < p.Rows; w++)
                for (int c = 0; c < p.Columns; c++)
                    if (p.Occupied(c, w))
                        _introCells.Add(new CrowdIntro.Cell
                        {
                            Index = p.Index(c, w),
                            Column = c,
                            Target = BoardSpace.ToWorld(_sim.Layout.CellToWorld(c, (vis - 1) - w))
                        });

            Rect area;
            if (_hasViewArea)
            {
                var a = _content.InverseTransformPoint(new Vector3(_viewArea.xMin, 0, _viewArea.yMin));
                var b = _content.InverseTransformPoint(new Vector3(_viewArea.xMax, 0, _viewArea.yMax));
                area = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            }
            else
            {
                // Chưa căn camera: lấy khung tranh nới rộng làm vùng nhìn thấy.
                var (fMin, fMax) = DefaultBoardLayout.FrameBounds();
                var c0 = BoardSpace.ToWorld(fMin);
                var c1 = BoardSpace.ToWorld(fMax);
                area = Rect.MinMaxRect(Mathf.Min(c0.x, c1.x) - 1, Mathf.Min(c0.z, c1.z) - 1, Mathf.Max(c0.x, c1.x) + 1, Mathf.Max(c0.z, c1.z) + 1);
            }
            int seed = p.Columns * 7919 + p.Rows * 104729 + p.Total;
            return new CrowdIntro(_visuals.Intro, _introCells, p.Columns, area, _cell, seed);
        }
    }
}
