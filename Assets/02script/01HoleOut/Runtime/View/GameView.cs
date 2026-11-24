using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Điều phối trình diễn một màn theo trạng thái mô phỏng, dùng art trong <see cref="VisualsSO"/>.
    /// Màn chơi chia ba nhóm: Picture (khay + tranh), Dock (khay chờ + hố), Board (bàn + khối); mỗi phần có view riêng:
    /// - <see cref="PictureView"/>: ô ngồi, nhân vật đứng chờ, cuộn tranh, cảnh chạy vào;
    /// - <see cref="RunnerView"/>: nhân vật chạy từ tranh và rơi vào hố;
    /// - <see cref="HoleView"/>: khối thoát thành hố, hố bay/đậu/đóng trên khay;
    /// - <see cref="BlockView"/>: khối trên bàn;
    /// - <see cref="DockView"/>: khay chờ và các chỗ.
    /// Pool nhân vật, hố và hiệu ứng giữ qua các màn; view theo màn dựng lại ở mỗi <see cref="Bind"/>.
    /// </summary>
    public sealed class GameView
    {
        const float TrayMargin = 0.28f;
        const float GroundY = -0.12f;
        const float GroundMeshSize = 10f;
        const float GroundViewportPadding = 0.01f;
        static readonly int GroundColorId = Shader.PropertyToID("_BaseColor");

        readonly Transform _root;
        readonly Transform _ground;
        readonly PictureVisuals _pictureVisuals;
        readonly DockVisuals _dockVisuals;
        readonly BoardVisuals _boardVisuals;
        readonly CharacterPool _characters;
        readonly HoleView _holes;
        readonly HoleEffects _holeEffects;

        // Vùng nhìn thấy trên mặt đất (x, z thế giới), cập nhật khi camera căn lại.
        Rect _viewArea;
        bool _hasViewArea;

        GameSimulation _sim;
        Transform _levelRoot;
        Transform _pictureGroup;
        BoardRig _board;
        DockView _dockView;
        PictureView _picture;
        RunnerView _runners;
        BlockView _blocks;
        float _cell = 0.76f;

        /// <summary>Đang chạy cảnh nhân vật vào tranh; controller khoá thao tác trong lúc này.</summary>
        public bool IntroPlaying => _sim != null && _picture.IntroPlaying;

        public GameView(Transform root, VisualsSO visuals)
        {
            _root = new GameObject("LevelView").transform;
            _root.SetParent(root, false);
            var palette = new ColorPalette(visuals);
            _pictureVisuals = new PictureVisuals(visuals, palette);
            _dockVisuals = new DockVisuals(visuals, palette);
            _boardVisuals = new BoardVisuals(visuals, palette);
            _characters = new CharacterPool(_root, _pictureVisuals);
            _holes = new HoleView(_dockVisuals, _characters.PoolRoot);
            _holeEffects = new HoleEffects(_dockVisuals, _characters.PoolRoot);
            _ground = CreateGround(_root, visuals);
        }

        static Transform CreateGround(Transform parent, VisualsSO visuals)
        {
            if (!visuals.groundMaterial) return null;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground";
            var t = ground.transform;
            t.SetParent(parent, false);
            t.rotation = Quaternion.Euler(0, 180, 0);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = visuals.groundMaterial;
            if (visuals.groundTint != Color.white && visuals.groundMaterial.HasProperty(GroundColorId))
            {
                var properties = new MaterialPropertyBlock();
                properties.SetColor(GroundColorId, visuals.groundMaterial.GetColor(GroundColorId) * visuals.groundTint);
                renderer.SetPropertyBlock(properties);
            }
            return t;
        }

        public void FitGround(Camera camera)
        {
            // Camera trực giao chỉ nghiêng theo X, nên hai góc đối diện đủ để căn mặt nền.
            var surface = new Plane(Vector3.up, new Vector3(0, GroundY, 0));
            var bottomLeft = camera.ViewportPointToRay(new Vector3(-GroundViewportPadding, -GroundViewportPadding, 0));
            var topRight = camera.ViewportPointToRay(new Vector3(1 + GroundViewportPadding, 1 + GroundViewportPadding, 0));
            if (!surface.Raycast(bottomLeft, out float near) || !surface.Raycast(topRight, out float far)) return;

            var min = bottomLeft.GetPoint(near);
            var max = topRight.GetPoint(far);
            _viewArea = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.z, max.z), Mathf.Max(min.x, max.x), Mathf.Max(min.z, max.z));
            _hasViewArea = true;
            _picture?.SetViewArea(_viewArea);
            if (!_ground) return;
            _ground.position = new Vector3((min.x + max.x) / 2, GroundY, (min.z + max.z) / 2);
            _ground.localScale = new Vector3((max.x - min.x) / GroundMeshSize, 1, (max.z - min.z) / GroundMeshSize);
        }

        // ───────────── Vòng đời ─────────────

        public void Bind(GameSimulation sim)
        {
            Clear();
            _sim = sim;
            _cell = (float)((DefaultBoardLayout)sim.Layout).Cell;
            float footprint = Mathf.Max(0.001f, _characters.SeatedFootprint);
            float agentScale = _cell * 0.92f / footprint;
            // Ô màu thấp để từng hàng còn rõ dưới camera nghiêng.
            float seatedWidth = _cell * _pictureVisuals.CellFill / footprint;
            float seatedHeight = _cell * _pictureVisuals.CellHeight / Mathf.Max(0.001f, _characters.SeatedHeight);
            var seatedScale = new Vector3(seatedWidth, seatedHeight, seatedWidth * _pictureVisuals.CellDepthRatio);
            var s0 = sim.Layout.DockSlot(0, Dock.MaxSlotsLimit);
            var s1 = sim.Layout.DockSlot(1, Dock.MaxSlotsLimit);
            float holeScale = _holes.UnitScale * (float)Vec2.Distance(s0, s1) * _dockVisuals.HoleFill;

            _levelRoot = new GameObject("Level").transform;
            _levelRoot.SetParent(_root, false);
            _pictureGroup = Group("Picture", _levelRoot);
            var dockGroup = Group("Dock", _levelRoot);

            // Picture: khay đứng yên, nội dung tranh cuộn trong gốc riêng.
            var picture = PictureTrayBounds();
            var tray = Object.Instantiate(_pictureVisuals.TrayPrefab, _pictureGroup);
            tray.name = "Tray";
            tray.SetBounds(picture.xMin, picture.xMax, picture.yMin, picture.yMax);
            if (!_pictureVisuals.ShowTrayFill)
            {
                tray.rimLayer.gameObject.SetActive(false);
                tray.HideFill();
            }
            var content = Group("Content", _pictureGroup);

            // Dock: khay chờ và các hố đang bay/đậu.
            var d0 = BoardSpace.ToWorld(sim.Layout.DockSlot(0, Dock.MaxSlotsLimit));
            float dockHalf = Mathf.Max(picture.xMax, -d0.x + 0.9f);
            _dockView = new DockView(dockGroup, dockHalf * 2, _dockVisuals);
            _dockView.Render(sim);

            // Board: bàn dựng sẵn trong prefab, chỉ tô lại các đoạn viền thuộc cổng.
            _board = Object.Instantiate(_boardVisuals.BoardPrefab, _levelRoot);
            _board.name = "Board";
            BoardSpace.ApplyLean(_board.transform);
            _board.ApplyGates(sim.Board.Gates, _boardVisuals.Palette);

            _holes.Bind(sim, dockGroup, holeScale);
            _picture = new PictureView(sim, _pictureVisuals, _characters, content, _cell, agentScale, seatedScale,
                Preferences.EntranceAnimation);
            if (_hasViewArea) _picture.SetViewArea(_viewArea);
            _runners = new RunnerView(sim, _characters, _picture, _holes, _pictureGroup, agentScale);
            _blocks = new BlockView(sim, _boardVisuals, _board);
        }

        public void Clear()
        {
            _picture?.Clear();
            _runners?.Clear();
            _holes.Clear();
            _holeEffects.Clear();
            _dockView?.Clear();
            if (_levelRoot) Object.Destroy(_levelRoot.gameObject);
            _picture = null;
            _runners = null;
            _blocks = null;
            _dockView = null;
            _levelRoot = null;
            _pictureGroup = null;
            _board = null;
            _sim = null;
        }

        public void Dispose()
        {
            Clear();
            _root.gameObject.SetActive(false);
            Object.Destroy(_root.gameObject);
        }

        // ───────────── Lệnh từ controller ─────────────

        /// <summary>Khối vừa thoát qua cổng khi kéo.</summary>
        public void NoteLaunch(BlockData block, DragResult drag)
        {
            if (drag.Exit == null) return;
            float extension = Mathf.Abs((float)(BoardRig.IsHorizontal(drag.Exit.side)
                ? drag.VisualY - drag.Y : drag.VisualX - drag.X));
            var start = BoardSpace.BoardToWorld(drag.VisualX, drag.VisualY, block.w, block.h, BlockView.DragY);
            _holes.AddExit(block, _blocks.Take(block.id), drag.Exit.side, start, extension, false);
        }

        /// <summary>Khối được booster nhấc: block.gx/gy đã là vị trí sát cổng, rig còn ở chỗ cũ.</summary>
        public void NoteLift(BlockData block, GateSide side)
        {
            var start = BoardSpace.BoardToWorld(block.gx, block.gy, block.w, block.h, BlockView.DragY);
            _holes.AddExit(block, _blocks.Take(block.id), side, start, 0, true);
        }

        /// <summary>Đánh dấu mục tiêu booster đang chờ chạm: khối (nhấc) hoặc hố trên khay (nam châm).</summary>
        public void SetBoosterTargeting(bool blocks, bool holes)
        {
            _blocks?.SetTargeting(blocks);
            _holes.SetTargeting(holes);
        }

        /// <summary>Làm nổi khối được hướng dẫn chỉ vào; -1 để tắt.</summary>
        public void SetTutorialFocus(int blockId) => _blocks?.SetFocus(blockId);

        /// <summary>Renderer của khối hướng dẫn và cổng cùng màu.</summary>
        public void CollectTutorialFocus(List<Renderer> into) => _blocks?.CollectFocus(into);

        /// <summary>Id hố đã đáp lên khay nằm dưới tia chạm; -1 nếu không trúng.</summary>
        public int HoleAt(Ray ray) => _holes.HoleAt(ray);

        // ───────────── Khung nội dung ─────────────

        /// <summary>Các góc bàn khối kể cả viền và cổng, theo hệ của mặt bàn (đã dựng nghiêng).</summary>
        public void BoardCorners(System.Collections.Generic.List<Vector3> corners)
        {
            corners.Clear();
            if (!_board) return;
            var inv = Quaternion.Inverse(BoardSpace.LeanRotation);
            var root = _board.transform.position;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in _board.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var lb = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var flat = inv * (r.transform.TransformPoint(c) - root);
                    min = Vector3.Min(min, flat); max = Vector3.Max(max, flat);
                }
            }
            if (min.x > max.x) return;
            for (int i = 0; i < 8; i++)
                corners.Add(root + BoardSpace.LeanRotation * new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
        }

        /// <summary>Khung nội dung theo Unity (x, z) để camera căn.</summary>
        public Rect ContentBounds()
        {
            var p = _sim.Picture;
            float zTop = -(float)_sim.Layout.CellToWorld(0, -0.6).Z;
            float zBottom = -(BoardSpace.BoardStartZ + 4 * BoardSpace.BoardCell) - 0.4f;
            float half = Mathf.Max(3.9f, p.Columns * _cell / 2 + 0.3f);
            return Rect.MinMaxRect(-half, zBottom, half, zTop);
        }

        /// <summary>
        /// Khung 3D thật của khay tranh, khay chờ và bàn khối (không gồm mặt đất),
        /// cộng chiều cao nhân vật ở hàng xa nhất để camera căn sát.
        /// </summary>
        public Bounds ContentWorldBounds()
        {
            var picture = PictureTrayBounds();
            var b = new Bounds(_pictureGroup.TransformPoint(new Vector3(picture.center.x, 0, picture.center.y)), Vector3.zero);
            b.Encapsulate(_pictureGroup.TransformPoint(new Vector3(picture.xMin, 0, picture.yMin)));
            b.Encapsulate(_pictureGroup.TransformPoint(new Vector3(picture.xMax, 0, picture.yMax)));
            foreach (var r in _levelRoot.GetComponentsInChildren<Renderer>())
            {
                // Bàn dựng nghiêng làm hộp bao trục thế giới phình to; góc bàn được tính riêng ở BoardCorners.
                if (r is ParticleSystemRenderer || (_board && r.transform.IsChildOf(_board.transform))) continue;
                b.Encapsulate(r.bounds);
            }
            // Nhân vật đứng cao khoảng một ô.
            b.Encapsulate(b.max + Vector3.up * _cell * 1.1f);
            return b;
        }

        Rect PictureTrayBounds()
        {
            var (frameMin, frameMax) = DefaultBoardLayout.FrameBounds();
            var c0 = BoardSpace.ToWorld(frameMin);
            var c1 = BoardSpace.ToWorld(frameMax);
            // Camera nhìn chéo: đỉnh ô hàng xa nhô lên che mép trên, nên mép xa chừa thêm đúng chiều cao ô.
            float farExtra = _cell * _pictureVisuals.CellHeight;
            return Rect.MinMaxRect(Mathf.Min(c0.x, c1.x) - TrayMargin, Mathf.Min(c0.z, c1.z) - TrayMargin,
                Mathf.Max(c0.x, c1.x) + TrayMargin, Mathf.Max(c0.z, c1.z) + TrayMargin + farExtra);
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ───────────── Sự kiện và vẽ mỗi frame ─────────────

        public void ProcessEvents(IReadOnlyList<GameEvent> events)
        {
            if (_sim == null) return;
            foreach (var e in events)
            {
                switch (e.Type)
                {
                    case GameEventType.HoleLaunched: _holes.OnLaunched(e.Id); break;
                    case GameEventType.HoleDocked: _holes.OnDocked(e.Id); break;
                    case GameEventType.HoleClosing: _holes.OnClosing(e.Id); break;
                    case GameEventType.HoleReleased: PlayCompleteFx(e.Id); _holes.OnReleased(e.Id); break;
                    case GameEventType.RunnerDispatched: _runners.OnDispatched(e.Id); break;
                    case GameEventType.RunnerLanded: _runners.OnLanded(e.Id); PlayCatchFx(e.Value); break;
                    case GameEventType.BlockBlocked: _blocks.PlayDockFull(e.Id); _dockView.PlayFull(Time.time); break;
                }
            }
        }

        // Hiệu ứng hố chỉ phản ánh sự kiện; bỏ hai lệnh gọi này không ảnh hưởng trạng thái hố.
        void PlayCompleteFx(int holeId)
        {
            if (_holes.TryGet(holeId, out var rig)) _holeEffects.PlayComplete(rig, _holes.ColorOf(holeId));
        }

        void PlayCatchFx(int holeId)
        {
            if (_holes.TryGet(holeId, out var rig)) _holeEffects.PlayCatch(rig, _holes.ColorOf(holeId), _holes.Scale);
        }

        public void Render(float time)
        {
            if (_sim == null) return;
            _dockView.Render(_sim);
            _dockView.RenderWarning(_sim, time);
            _dockView.RenderFullAlert(Time.time);
            _blocks.Render();
            _holes.Render();
            _picture.Render(time);
            _runners.Render();
        }
    }
}
