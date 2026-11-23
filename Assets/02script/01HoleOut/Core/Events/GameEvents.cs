namespace HoleOut.Core
{
    public enum GameEventType
    {
        BlockMoved,
        /// <summary>Khối chạm cổng cùng màu nhưng khay chờ đầy (Id = khối).</summary>
        BlockBlocked,
        BlockExited,
        BlockRevealed,
        WaveSpawned,
        HoleLaunched,
        HoleDocked,
        RunnerDispatched,
        RunnerLanded,
        HoleClosing,
        HoleReleased,
        PictureScrolled,
        Won,
        Jammed,
        Continued,
        BlockLifted,
        SlotAdded,
        MagnetPulled,
    }

    /// <summary>Sự kiện mô phỏng. Id/Value tùy loại: id khối/hố/nhân vật, số đợt, hàng cuộn...</summary>
    public readonly struct GameEvent
    {
        public readonly GameEventType Type;
        public readonly int Id;
        public readonly int Value;
        public readonly double Time;

        public GameEvent(GameEventType type, int id, int value, double time)
        {
            Type = type; Id = id; Value = value; Time = time;
        }

        public override string ToString() => $"{Type}({Id},{Value})@{Time:0.000}";
    }
}
