using System.Collections.Generic;

namespace HoleOut.Core
{
    /// <summary>Bàn khối 6×4 với các đợt khối và cổng màu.</summary>
    public sealed class BlockBoard
    {
        public const int MysteryRevealBlockCount = 7;

        public readonly List<BlockData> Blocks = new List<BlockData>();
        public readonly List<GateData> Gates = new List<GateData>();
        readonly Queue<List<BlockData>> _pendingWaves = new Queue<List<BlockData>>();

        public int WaveTotal { get; }
        public int WaveNumber { get; private set; }
        public int PendingWaves => _pendingWaves.Count;

        public BlockBoard(LevelData level)
        {
            foreach (var g in level.gates) Gates.Add(g);
            for (int i = 0; i < level.waves.Count; i++)
            {
                var list = new List<BlockData>();
                foreach (var b in level.waves[i].blocks) list.Add(b.Copy());
                if (i == 0) Blocks.AddRange(list);
                else _pendingWaves.Enqueue(list);
            }
            WaveTotal = level.waves.Count;
            WaveNumber = 1;
        }

        public BlockData Find(int id)
        {
            for (int i = 0; i < Blocks.Count; i++) if (Blocks[i].id == id) return Blocks[i];
            return null;
        }

        public GateData FindExitGate(BlockData b, GateSide? preferred)
        {
            if (preferred.HasValue) return DragSolver.FindGate(b, b.gx, b.gy, Gates, preferred.Value);
            for (int s = 0; s < 4; s++)
            {
                var g = DragSolver.FindGate(b, b.gx, b.gy, Gates, (GateSide)s);
                if (g != null) return g;
            }
            return null;
        }

        public void Remove(BlockData b) => Blocks.Remove(b);

        /// <summary>Khi khối cuối đợt rời bàn thì đợt kế tiếp xuất hiện.</summary>
        public bool TrySpawnNextWave()
        {
            if (Blocks.Count > 0 || _pendingWaves.Count == 0) return false;
            Blocks.AddRange(_pendingWaves.Dequeue());
            WaveNumber++;
            return true;
        }

        /// <summary>
        /// Luật băng: mỗi khi một khối rời bàn, mọi khối đóng băng còn lại trên bàn giảm một lớp.
        /// Khối của đợt sau chưa lên bàn nên không bị tính. Ghi id khối vừa giảm vào <paramref name="changed"/> nếu có.
        /// </summary>
        public static void ThawAfterExit(List<BlockData> blocks, List<int> changed = null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                var b = blocks[i];
                if (!b.Frozen) continue;
                b.ice--;
                changed?.Add(b.id);
            }
        }

        /// <summary>
        /// Khối bí ẩn lộ màu khi dịch được ít nhất một ô theo một hướng, hoặc bàn còn tối đa 7 khối.
        /// Ghi id khối vừa lộ vào <paramref name="revealed"/>.
        /// </summary>
        public void RevealMysteries(List<int> revealed)
        {
            for (int i = 0; i < Blocks.Count; i++)
            {
                var b = Blocks[i];
                if (!b.mystery) continue;
                bool canMove =
                    DragSolver.Free(b, b.gx - 1, b.gy, Blocks) || DragSolver.Free(b, b.gx + 1, b.gy, Blocks) ||
                    DragSolver.Free(b, b.gx, b.gy - 1, Blocks) || DragSolver.Free(b, b.gx, b.gy + 1, Blocks);
                if (canMove || Blocks.Count <= MysteryRevealBlockCount)
                {
                    b.mystery = false;
                    revealed.Add(b.id);
                }
            }
        }
    }
}
