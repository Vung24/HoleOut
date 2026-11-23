using System.Collections.Generic;

namespace HoleOut.Core
{
    public enum HoleState { Flying, Docked, Closing, Released }

    public sealed class Hole
    {
        public int Id;
        public string Color;
        public int InitialCapacity;
        /// <summary>Số nhân vật hố còn phải nhận (giảm khi nhân vật đáp).</summary>
        public int RemainingCapacity;
        /// <summary>Số chỗ đã được nhân vật đứng dậy giữ (tính lại mỗi lượt giữ chỗ).</summary>
        public int Held;
        /// <summary>Số nhân vật đã xuất phát và đang tới.</summary>
        public int Incoming;
        public int SlotIndex;
        public HoleState State;
        /// <summary>Flying: tiến độ bay (bắt đầu âm để trễ cất cánh). Closing: thời gian đã đóng.</summary>
        public double Timer;
        /// <summary>Giá trị Timer lúc hố bắt đầu bay; nhỏ hơn FlightStart khi khối được nhấc qua bàn trước.</summary>
        public double LaunchStart = Dock.FlightStart;

        /// <summary>Chỗ còn nhận lượt giữ hoặc xuất phát mới, không âm.</summary>
        public int Free => System.Math.Max(0, RemainingCapacity - Held - Incoming);
        /// <summary>Chỗ chưa có nhân vật đang tới (bỏ qua giữ chỗ).</summary>
        public int Unclaimed => RemainingCapacity - Incoming;
    }

    /// <summary>Khay chờ: hố bay lên chiếm chỗ ngay; hố đóng vẫn chiếm chỗ tới khi trả.</summary>
    public sealed class Dock
    {
        public const int MaxSlotsLimit = 6;
        public const double FlightRate = 4.8;
        public const double FlightStart = -0.8;
        public const double CloseDuration = 0.66;

        public int SlotCount { get; private set; }
        /// <summary>Hố đã đáp theo thứ tự lên khay (gồm cả hố đang đóng).</summary>
        public readonly List<Hole> Docked = new List<Hole>();
        public readonly List<Hole> Flying = new List<Hole>();

        public Dock(int slots) { SlotCount = slots; }

        public int Occupied => Docked.Count + Flying.Count;
        public bool IsFull => Occupied >= SlotCount;
        public bool CanAddSlot => SlotCount < MaxSlotsLimit;

        public bool AddSlot()
        {
            if (!CanAddSlot) return false;
            SlotCount++;
            return true;
        }

        public bool AnyClosing()
        {
            for (int i = 0; i < Docked.Count; i++) if (Docked[i].State == HoleState.Closing) return true;
            return false;
        }

        public Hole FindDocked(int id)
        {
            for (int i = 0; i < Docked.Count; i++) if (Docked[i].Id == id) return Docked[i];
            return null;
        }

        /// <summary>Chỗ trống đầu tiên, tính cả hố trên khay và đang bay. -1 nếu đầy.</summary>
        public int FirstFreeSlot()
        {
            if (IsFull) return -1;
            for (int s = 0; s < SlotCount; s++)
            {
                bool used = false;
                for (int i = 0; i < Docked.Count && !used; i++) used = Docked[i].SlotIndex == s;
                for (int i = 0; i < Flying.Count && !used; i++) used = Flying[i].SlotIndex == s;
                if (!used) return s;
            }
            return -1;
        }

        public Hole Launch(BlockData block, double launchStart = FlightStart)
        {
            int slot = FirstFreeSlot();
            if (slot < 0) return null;
            var hole = new Hole
            {
                Id = block.id, Color = block.color, InitialCapacity = block.capacity,
                RemainingCapacity = block.capacity, SlotIndex = slot,
                State = HoleState.Flying, Timer = launchStart, LaunchStart = launchStart
            };
            Flying.Add(hole);
            return hole;
        }
    }
}
