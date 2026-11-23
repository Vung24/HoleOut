using System;
using System.Collections.Generic;

namespace HoleOut.Core
{
    public enum GameStatus { Playing, Won, Jammed }

    /// <summary>
    /// Mô phỏng một lần chơi. View đọc trạng thái và gửi lệnh; mọi luật (giữ chỗ, lịch đáp,
    /// đóng hố, kết thúc) quyết định ở đây theo đồng hồ mô phỏng.
    ///
    /// Thứ tự một lần <see cref="Step"/>:
    ///  1. tăng đồng hồ;
    ///  2. tiến cửa sổ tranh tới hàng gần nhất còn nhân vật;
    ///  3. giữ chỗ cho nhân vật lộ;
    ///  4. diễn hoạt cuộn tranh và đứng dậy/ngồi xuống;
    ///  5. hố đang bay tới khay;
    ///  6. hố đang đóng: trả chỗ và kiểm tra kết thúc;
    ///  7. xuất phát mọi nhân vật sẵn sàng trong cùng lượt;
    ///  8. nhân vật đáp: giảm sức chứa, bắt đầu đóng hố, kiểm tra kết thúc;
    ///  9. nếu không còn hố bay và nhân vật đang chạy: kiểm tra kết thúc.
    /// </summary>
    public sealed class GameSimulation
    {
        public const int MaxRunnersTotal = 64;
        public const int MaxRunnersPerHole = 32;
        public const int MaxDispatchPerStep = 64;
        public const double StandReady = 0.99;

        public readonly LevelData Level;
        public readonly BlockBoard Board;
        public readonly Picture Picture;
        public readonly Dock Dock;
        public readonly List<Runner> Runners = new List<Runner>();
        public readonly MotionConfig Motion;
        public readonly IBoardLayout Layout;

        public GameStatus Status { get; private set; } = GameStatus.Playing;
        public double Time { get; private set; }
        public int Cleared { get; private set; }
        public int ContinueCount { get; private set; }

        readonly List<GameEvent> _events = new List<GameEvent>();
        readonly List<int> _revealBuffer = new List<int>();
        readonly SeededRandom _rng;
        readonly DiveBag _dives;
        int _runnerSerial;

        // Trạng thái kéo hiện tại.
        int _dragId = -1;
        double _dragX, _dragY;
        bool _hasDragResult;
        DragResult _lastDrag;

        public GameSimulation(LevelData level, int seed = 1, IBoardLayout layout = null, MotionConfig motion = null)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Board = new BlockBoard(level);
            Picture = new Picture(level);
            Dock = new Dock(level.dockSlots > 0 ? level.dockSlots : 5);
            Motion = motion ?? new MotionConfig();
            Layout = layout ?? new DefaultBoardLayout(Picture.Columns, Picture.VisibleRows, level.pictureScale);
            _rng = new SeededRandom(seed);
            _dives = new DiveBag(Motion.DiveDurations.Length);
            Picture.UpdateExposure();
            RevealMysteries();
        }

        // ───────────── Sự kiện ─────────────

        public IReadOnlyList<GameEvent> Events => _events;
        public void ClearEvents() => _events.Clear();
        void Emit(GameEventType t, int id = 0, int value = 0) => _events.Add(new GameEvent(t, id, value, Time));

        // ───────────── Kéo khối ─────────────

        public int DraggingBlockId => _dragId;
        public double DragX => _dragX;
        public double DragY => _dragY;
        public DragResult LastDrag => _lastDrag;
        /// <summary>Đã có ít nhất một lần DragTo trong lượt kéo hiện tại.</summary>
        public bool HasDragResult => _dragId >= 0 && _hasDragResult;

        public bool BeginDrag(int blockId)
        {
            if (Status != GameStatus.Playing || _dragId >= 0) return false;
            var b = Board.Find(blockId);
            if (b == null) return false;
            _dragId = blockId;
            _dragX = b.gx;
            _dragY = b.gy;
            _hasDragResult = false;
            return true;
        }

        /// <summary>Kéo khối đang chọn tới vị trí đích (đơn vị ô). Có thể làm khối thoát qua cổng.</summary>
        public DragResult DragTo(double targetX, double targetY)
        {
            var b = Board.Find(_dragId);
            if (b == null || Status != GameStatus.Playing) { CancelDrag(); return default; }

            var r = DragSolver.Drag(b, Board.Blocks, Board.Gates, _dragX, _dragY, targetX, targetY, Dock.IsFull);
            int prevSnapX = _hasDragResult ? _lastDrag.SnapX : b.gx, prevSnapY = _hasDragResult ? _lastDrag.SnapY : b.gy;
            bool wasDockFull = _hasDragResult && _lastDrag.DockFull;
            _dragX = r.X; _dragY = r.Y; _lastDrag = r; _hasDragResult = true;
            if (r.SnapX != prevSnapX || r.SnapY != prevSnapY) Emit(GameEventType.BlockMoved, b.id);
            if (r.DockFull && !wasDockFull) Emit(GameEventType.BlockBlocked, b.id);

            if (r.Exit != null)
            {
                b.gx = r.SnapX; b.gy = r.SnapY;
                if (LaunchBlock(b, r.Exit.side)) _dragId = -1;
            }
            return r;
        }

        /// <summary>Thả khối: khớp ô gần nhất hợp lệ. Trả về true nếu vị trí khối thay đổi.</summary>
        public bool EndDrag()
        {
            var b = Board.Find(_dragId);
            _dragId = -1;
            if (b == null || !_hasDragResult) return false;
            bool moved = b.gx != _lastDrag.SnapX || b.gy != _lastDrag.SnapY;
            b.gx = _lastDrag.SnapX; b.gy = _lastDrag.SnapY;
            _hasDragResult = false;
            RevealMysteries();
            return moved;
        }

        public void CancelDrag()
        {
            _dragId = -1;
            _hasDragResult = false;
        }

        bool LaunchBlock(BlockData b, GateSide side, double launchStart = Dock.FlightStart)
        {
            if (Dock.IsFull) return false;
            var gate = Board.FindExitGate(b, side);
            if (gate == null) return false;
            Board.Remove(b);
            Emit(GameEventType.BlockExited, b.id, (int)side);
            if (Board.TrySpawnNextWave()) Emit(GameEventType.WaveSpawned, 0, Board.WaveNumber);
            RevealMysteries();
            var hole = Dock.Launch(b, launchStart);
            Emit(GameEventType.HoleLaunched, hole.Id, hole.SlotIndex);
            return true;
        }

        void RevealMysteries()
        {
            _revealBuffer.Clear();
            Board.RevealMysteries(_revealBuffer);
            foreach (var id in _revealBuffer) Emit(GameEventType.BlockRevealed, id);
        }

        // ───────────── Tiếp tục sau kẹt ─────────────

        /// <summary>Cấp thêm một chỗ khay và quay lại Playing. Chỉ thành công khi đang kẹt và khay chưa tối đa.</summary>
        public bool TryContinueWithExtraSlot()
        {
            if (Status != GameStatus.Jammed || !Dock.AddSlot()) return false;
            Status = GameStatus.Playing;
            ContinueCount++;
            Emit(GameEventType.Continued, 0, Dock.SlotCount);
            return true;
        }

        // ───────────── Booster ─────────────

        /// <summary>Thời gian khối bay qua bàn tới cổng khi được nhấc (giây).</summary>
        public const double LiftDuration = 0.45;

        /// <summary>Booster chỉ dùng khi đang chơi và không có khối đang kéo.</summary>
        public bool CanUseBooster => Status == GameStatus.Playing && _dragId < 0;

        /// <summary>
        /// Nhấc khối: khối bay qua mọi vật cản tới vị trí sát cổng cùng màu gần nhất rồi thoát như bình thường.
        /// Cần khay còn chỗ; khối bí ẩn chưa lộ màu không nhấc được.
        /// </summary>
        public bool TryLiftBlock(int blockId, out GateSide side)
        {
            side = GateSide.Top;
            if (!CanUseBooster || Dock.IsFull) return false;
            var b = Board.Find(blockId);
            if (b == null || b.mystery || !FindLiftExit(b, out int gx, out int gy, out side)) return false;
            b.gx = gx;
            b.gy = gy;
            Emit(GameEventType.BlockLifted, b.id, (int)side);
            return LaunchBlock(b, side, Dock.FlightStart - LiftDuration * Dock.FlightRate);
        }

        /// <summary>Vị trí sát mép có cổng nhận khối, gần vị trí hiện tại nhất (khoảng cách ô theo trục).</summary>
        bool FindLiftExit(BlockData b, out int bestX, out int bestY, out GateSide bestSide)
        {
            bestX = bestY = 0;
            bestSide = GateSide.Top;
            int best = int.MaxValue;
            for (int s = 0; s < 4; s++)
            {
                var side = (GateSide)s;
                bool vertical = side == GateSide.Top || side == GateSide.Bottom;
                int steps = vertical ? DragSolver.BoardWidth - b.w : DragSolver.BoardHeight - b.h;
                for (int i = 0; i <= steps; i++)
                {
                    int x = vertical ? i : side == GateSide.Left ? 0 : DragSolver.BoardWidth - b.w;
                    int y = !vertical ? i : side == GateSide.Top ? 0 : DragSolver.BoardHeight - b.h;
                    if (DragSolver.FindGate(b, x, y, Board.Gates, side) == null) continue;
                    int d = Math.Abs(x - b.gx) + Math.Abs(y - b.gy);
                    if (d >= best) continue;
                    best = d; bestX = x; bestY = y; bestSide = side;
                }
            }
            return best < int.MaxValue;
        }

        /// <summary>Thêm một chỗ khay trong lúc chơi (tối đa <see cref="Dock.MaxSlotsLimit"/>).</summary>
        public bool TryAddSlot()
        {
            if (!CanUseBooster) return false;
            int before = Dock.SlotCount;
            if (!Dock.AddSlot()) return false;
            // Vị trí hố dồn lại theo số chỗ mới: dời điểm đáp của nhân vật đang chạy theo hố của mình.
            foreach (var r in Runners)
            {
                var h = Dock.FindDocked(r.HoleId);
                if (h == null) continue;
                var from = Layout.DockSlot(h.SlotIndex, before);
                var to = Layout.DockSlot(h.SlotIndex, Dock.SlotCount);
                double dx = to.X - from.X, dz = to.Z - from.Z;
                r.Target = Shift(r.Target, dx, dz);
                r.Entry = Shift(r.Entry, dx, dz);
                int last = r.Route.Count - 1;
                r.Route[last] = Shift(r.Route[last], dx, dz);
                double dist = 0;
                for (int i = 1; i < r.Route.Count; i++) dist += Vec2.Distance(r.Route[i - 1], r.Route[i]);
                r.RunDistance = dist;
            }
            Emit(GameEventType.SlotAdded, 0, Dock.SlotCount);
            return true;
        }

        static Vec2 Shift(Vec2 p, double dx, double dz) => new Vec2(p.X + dx, p.Z + dz);

        /// <summary>
        /// Nam châm: nhân vật cùng màu trong cửa sổ tranh (kể cả chưa có lối ra) bay thẳng vào hố đã chọn
        /// tới khi đủ chỗ còn trống. Nhân vật đã giữ chỗ ở hố khác giữ nguyên lượt. Trả về số nhân vật được hút.
        /// </summary>
        public int TryMagnet(int holeId)
        {
            if (!CanUseBooster || !Picture.IsSettled) return 0;
            var h = Dock.FindDocked(holeId);
            if (h == null || h.State != HoleState.Docked) return 0;
            Reserve();
            var p = Picture;
            int room = Math.Min(h.Free, Math.Min(MaxRunnersPerHole - CountRunners(h.Id), MaxRunnersTotal - Runners.Count));
            int pulled = 0;
            for (int w = p.WindowLow; w <= p.WindowHigh && w < p.Rows && pulled < room; w++)
                for (int c = 0; c < p.Columns && pulled < room; c++)
                {
                    if (!p.Occupied(c, w) || p.ColorAt(c, w) != h.Color || p.ReadyHole(c, w) >= 0) continue;
                    var runner = PlanRunner(c, w, h, true);
                    double stagger = Math.Min(MotionConfig.MaxBatchStagger, pulled * MotionConfig.BatchStagger);
                    runner.AirStartAt += stagger;
                    runner.AirEndAt += stagger;
                    // Hết làn đáp trong thời gian chờ cho phép: dừng hút, phần còn lại ở yên trong tranh.
                    if (!ScheduleDive(runner, h)) { room = pulled; break; }
                    p.Remove(c, w);
                    h.Incoming++;
                    runner.DispatchedAt = Time;
                    Runners.Add(runner);
                    Emit(GameEventType.RunnerDispatched, runner.Id, h.Id);
                    pulled++;
                }
            if (pulled == 0) return 0;
            p.UpdateExposure();
            Emit(GameEventType.MagnetPulled, h.Id, pulled);
            return pulled;
        }

        // ───────────── Vòng cập nhật ─────────────

        public void Step(double dt)
        {
            if (dt < 0) dt = 0;
            Time += dt;
            if (Status != GameStatus.Playing) return;

            if (Picture.AdvanceWindow()) Emit(GameEventType.PictureScrolled, 0, Picture.RowOffset);

            Reserve();
            Picture.AnimateScroll(dt);
            Picture.UpdateStand(dt);

            for (int i = Dock.Flying.Count - 1; i >= 0; i--)
            {
                var h = Dock.Flying[i];
                h.Timer += dt * Dock.FlightRate;
                if (h.Timer < 1) continue;
                h.State = HoleState.Docked;
                h.Timer = 0;
                Dock.Flying.RemoveAt(i);
                Dock.Docked.Add(h);
                Emit(GameEventType.HoleDocked, h.Id, h.SlotIndex);
            }

            for (int i = Dock.Docked.Count - 1; i >= 0; i--)
            {
                var h = Dock.Docked[i];
                if (h.State != HoleState.Closing) continue;
                h.Timer += dt;
                if (h.Timer < Dock.CloseDuration) continue;
                h.State = HoleState.Released;
                Dock.Docked.RemoveAt(i);
                Emit(GameEventType.HoleReleased, h.Id, h.SlotIndex);
                CheckEnd();
            }

            if (Status == GameStatus.Playing)
            {
                for (int batch = 0; batch < MaxDispatchPerStep; batch++)
                {
                    if (!TryDispatch(batch)) break;
                    Reserve();
                }
            }

            for (int i = Runners.Count - 1; i >= 0; i--)
            {
                var r = Runners[i];
                r.Elapsed = Math.Min(r.Duration, r.Elapsed + dt);
                if (r.Elapsed < r.Duration) continue;
                Runners.RemoveAt(i);
                Cleared++;
                var h = Dock.FindDocked(r.HoleId);
                if (h != null)
                {
                    h.RemainingCapacity--;
                    h.Incoming = Math.Max(0, h.Incoming - 1);
                    if (h.RemainingCapacity <= 0) BeginClose(h);
                }
                Emit(GameEventType.RunnerLanded, r.Id, r.HoleId);
                CheckEnd();
            }

            if (Dock.Flying.Count == 0 && Runners.Count == 0) CheckEnd();
        }

        void BeginClose(Hole h)
        {
            if (h.State == HoleState.Closing) return;
            h.RemainingCapacity = 0;
            h.State = HoleState.Closing;
            h.Timer = 0;
            Emit(GameEventType.HoleClosing, h.Id, h.SlotIndex);
        }

        // ───────────── Giữ chỗ ─────────────

        Hole FirstFreeHole(string color)
        {
            foreach (var h in Dock.Docked)
                if (h.State == HoleState.Docked && h.RemainingCapacity > 0 && h.Color == color && h.Free > 0) return h;
            return null;
        }

        /// <summary>
        /// Nhân vật lộ, đã ổn định, cùng màu hố đã lên khay còn chỗ được giữ chỗ.
        /// Hố trùng màu được phục vụ theo thứ tự trên khay; sang hố sau khi mọi chỗ hố trước đã được giữ/nhận.
        /// </summary>
        void Reserve()
        {
            foreach (var h in Dock.Docked) h.Held = 0;
            Picture.UpdateExposure();
            var p = Picture;

            for (int c = 0; c < p.Columns; c++)
                for (int w = 0; w < p.Rows; w++)
                {
                    if (!p.Occupied(c, w)) continue;
                    int ready = p.ReadyHole(c, w);
                    if (!p.CanLeave(c, w)) { if (ready >= 0) p.SetReadyHole(c, w, -1); continue; }
                    if (ready < 0) continue;
                    var h = Dock.FindDocked(ready);
                    if (h != null && FirstFreeHole(p.ColorAt(c, w)) == h) h.Held++;
                    else p.SetReadyHole(c, w, -1);
                }

            for (int w = 0; w < p.Rows; w++)
                for (int c = 0; c < p.Columns; c++)
                {
                    if (!p.Occupied(c, w) || !p.CanLeave(c, w) || p.ReadyHole(c, w) >= 0) continue;
                    var h = FirstFreeHole(p.ColorAt(c, w));
                    if (h == null) continue;
                    p.SetReadyHole(c, w, h.Id);
                    h.Held++;
                }
        }

        /// <summary>Có hố cùng màu trên khay còn chỗ hoặc đang bay tới (dùng cho trình diễn đứng dậy sớm).</summary>
        public bool HasHoleFor(string color)
        {
            foreach (var h in Dock.Docked) if (h.State == HoleState.Docked && h.Color == color && h.Unclaimed > 0) return true;
            foreach (var h in Dock.Flying) if (h.Color == color) return true;
            return false;
        }

        // ───────────── Xuất phát ─────────────

        bool TryDispatch(int batch)
        {
            var p = Picture;
            int bc = -1, bw = -1;
            Hole hole = null;
            double bestStand = -1;
            // Nhân vật đã đứng dậy lâu hơn chạy trước; cùng mức thì hàng gần mép thoát trước.
            for (int w = p.WindowLow; w <= p.WindowHigh && w < p.Rows; w++)
                for (int c = 0; c < p.Columns; c++)
                {
                    if (!p.Occupied(c, w) || !p.CanLeave(c, w)) continue;
                    int ready = p.ReadyHole(c, w);
                    if (ready < 0) continue;
                    var h = Dock.FindDocked(ready);
                    if (h == null || h.Color != p.ColorAt(c, w) || h.Unclaimed <= 0) continue;
                    double s = p.Stand(c, w);
                    if (s <= bestStand) continue;
                    bc = c; bw = w; hole = h; bestStand = s;
                }
            if (hole == null) return false;

            // Lượt đầu chờ nhân vật đứng hẳn; các lượt sau trong cùng đợt nối đuôi ngay.
            if (batch == 0 && p.Stand(bc, bw) < StandReady) return false;
            if (Runners.Count >= MaxRunnersTotal || CountRunners(hole.Id) >= MaxRunnersPerHole) return false;

            var runner = PlanRunner(bc, bw, hole);
            double stagger = Math.Min(MotionConfig.MaxBatchStagger, batch * MotionConfig.BatchStagger);
            runner.AirStartAt += stagger;
            runner.AirEndAt += stagger;
            if (!ScheduleDive(runner, hole)) return false;

            p.Remove(bc, bw);
            p.UpdateExposure();
            hole.Incoming++;
            runner.DispatchedAt = Time;
            Runners.Add(runner);
            Emit(GameEventType.RunnerDispatched, runner.Id, hole.Id);
            return true;
        }

        int CountRunners(int holeId)
        {
            int n = 0;
            foreach (var r in Runners) if (r.HoleId == holeId) n++;
            return n;
        }

        Runner PlanRunner(int col, int w, Hole hole, bool lifted = false)
        {
            var r = new Runner
            {
                Id = ++_runnerSerial, Column = col, SourceRow = Picture.SourceRow(w), Color = Picture.ColorAt(col, w),
                HoleId = hole.Id, Variant = _dives.Pick(_rng), Target = Layout.DockSlot(hole.SlotIndex, Dock.SlotCount),
                Lifted = lifted
            };
            Prepare(r, r.Target, MotionConfig.PlanLedgeOffset);
            r.AirStartAt = Time + r.PreAir;
            r.AirEndAt = r.AirStartAt + r.AirDuration;
            return r;
        }

        /// <summary>Chọn làn đáp sớm nhất: cùng làn, lượt sau bay không sớm hơn lúc lượt trước đáp + 0,04 giây.</summary>
        bool ScheduleDive(Runner r, Hole hole)
        {
            double natural = r.AirStartAt;
            int bestLane = 0;
            double bestStart = double.MaxValue;
            for (int lane = 0; lane < MotionConfig.EntryLanes; lane++)
            {
                double start = natural;
                foreach (var o in Runners)
                    if (o.HoleId == r.HoleId && o.EntryLane == lane) start = Math.Max(start, o.AirEndAt + MotionConfig.LaneGap);
                if (start < bestStart) { bestStart = start; bestLane = lane; }
            }
            r.EntryLane = bestLane;
            double angle = bestLane * Math.PI / 4 + Math.PI / 8;
            r.Entry = new Vec2(r.Target.X + Math.Cos(angle) * MotionConfig.EntryRadius, r.Target.Z + Math.Sin(angle) * MotionConfig.EntryRadius);
            r.Finale = hole.Unclaimed == 1;
            Prepare(r, r.Entry, MotionConfig.LedgeOffset);

            double actualNatural = Time + r.PreAir;
            r.AirStartAt = Math.Max(bestStart, actualNatural);
            double extra = r.AirStartAt - actualNatural;
            // Chờ làn đáp ở điểm xuất phát rồi chạy hết tốc; không giảm tốc chạy để kéo dài lịch.
            if (extra > MotionConfig.MaxLaneWait) return false;
            r.StartDelay = extra;
            r.AirEndAt = r.AirStartAt + r.AirDuration;
            r.Duration = r.PreAir + r.AirDuration + extra;
            return true;
        }

        /// <summary>Chọn đường chạy tới mép hố và tính các pha thời gian.</summary>
        void Prepare(Runner r, Vec2 target, double ledgeOffset)
        {
            var p = Picture;
            int col = r.Column, w = p.Rows - 1 - r.SourceRow;
            int bottomRow = p.VisibleRows - 1;
            int visualRow = bottomRow - w + p.RowOffset;
            var start = Layout.CellToWorld(col, visualRow);
            double runOffset = ledgeOffset - MotionConfig.RouteOvershoot;
            var end = Toward(target, start, runOffset);
            var nextColumn = Layout.CellToWorld(col == 0 ? 1 : col - 1, visualRow);
            double cellWidth = col == 0 ? nextColumn.X - start.X : start.X - nextColumn.X;
            double cellDepth = Layout.CellToWorld(col, visualRow + 1).Z - start.Z;
            r.Route.Clear();
            r.Route.Add(start);
            // Nhân vật được nam châm hút bay thẳng tới hố, bỏ qua kiểm tra lối đi.
            if (!r.Lifted && (cellWidth <= 0 || cellDepth <= 0 ||
                !p.CanRunStraight(col, w, col + (end.X - start.X) / cellWidth, w - (end.Z - start.Z) / cellDepth)))
            {
                int lastCol = col;
                var cells = p.FindExitPath(col, w);
                if (cells != null)
                    foreach (var (cc, ww) in cells)
                    {
                        r.Route.Add(Layout.CellToWorld(cc, bottomRow - ww + p.RowOffset));
                        lastCol = cc;
                    }
                var exit = Layout.CellToWorld(lastCol, p.VisibleRows - 0.25);
                r.Route.Add(exit);
                end = Toward(target, exit, runOffset);
            }
            r.Route.Add(end);

            double dist = 0;
            for (int i = 1; i < r.Route.Count; i++) dist += Vec2.Distance(r.Route[i - 1], r.Route[i]);
            r.RunDistance = dist;
            double run = Math.Max(Motion.MinRunDuration, dist / Motion.RunSpeed);
            r.AirDuration = Motion.DiveDurations[r.Variant];
            if (!r.TripRolled)
            {
                r.TripRolled = true;
                r.Tripped = !r.Lifted && dist > Motion.TripMinDistance && _rng.NextDouble() < Motion.TripChance;
            }
            double trip = r.Tripped ? Motion.Trip : 0;
            double tease = r.Finale ? Motion.Tease : 0;
            r.PreAir = Motion.Ready + run + trip + Motion.Brake + tease + Motion.Crouch;
            r.Duration = r.PreAir + r.AirDuration;
            r.Elapsed = 0;
        }

        static Vec2 Toward(Vec2 origin, Vec2 toward, double distance)
        {
            double dx = toward.X - origin.X, dz = toward.Z - origin.Z, d = Math.Max(0.001, Math.Sqrt(dx * dx + dz * dz));
            return new Vec2(origin.X + dx / d * distance, origin.Z + dz / d * distance);
        }

        // ───────────── Kết thúc ─────────────

        /// <summary>Chỉ đánh giá trong Playing; hoãn cả thắng và kẹt khi có hố đang đóng.</summary>
        void CheckEnd()
        {
            if (Status != GameStatus.Playing || Dock.AnyClosing()) return;

            if (Picture.Remaining == 0 && Runners.Count == 0)
            {
                Status = GameStatus.Won;
                CancelDrag();
                Emit(GameEventType.Won);
                return;
            }

            if (Dock.Docked.Count >= Dock.SlotCount && Dock.Flying.Count == 0 && Runners.Count == 0)
            {
                Picture.UpdateExposure();
                var p = Picture;
                for (int w = 0; w < p.Rows; w++)
                    for (int c = 0; c < p.Columns; c++)
                    {
                        if (!p.IsExposed(c, w)) continue;
                        string color = p.ColorAt(c, w);
                        foreach (var h in Dock.Docked)
                            if (h.Color == color && h.Unclaimed > 0) return;
                    }
                Status = GameStatus.Jammed;
                CancelDrag();
                Emit(GameEventType.Jammed);
            }
        }
    }
}
