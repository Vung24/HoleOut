using System.Collections.Generic;

namespace HoleOut.Core
{
    /// <summary>Nhịp chuyển động dùng cho lịch chạy/nhảy. Giá trị mặc định là cấu hình thử nghiệm.</summary>
    public sealed class MotionConfig
    {
        public double RunSpeed = 6.5;
        public double MinRunDuration = 0.3;
        public double Ready = 0.24;
        public double Brake = 0.14;
        public double Crouch = 0.12;
        /// <summary>Nhịp khựng ở mép của con cuối vào hố trước khi nhảy.</summary>
        public double Tease = 0.36;
        public double Trip = 0.55;
        public double TripChance = 0.14;
        public double TripMinDistance = 2.2;
        /// <summary>Thời gian bay của từng kiểu nhảy.</summary>
        public double[] DiveDurations = { .44, .42, .5, .46, .44, .44, .46, .46, .44, .48, .5 };

        public const int EntryLanes = 8;
        public const double LaneGap = 0.04;
        public const double MaxLaneWait = 1.8;
        public const double BatchStagger = 0.06;
        public const double MaxBatchStagger = 0.5;
        // Chừa khoảng hở cho thân và đoạn tiến ngang cuối khi nhân vật hạ xuống miệng hố.
        public const double EntryRadius = 0.15;
        public const double LedgeOffset = 0.58;
        public const double PlanLedgeOffset = 0.44;
        public const double RouteOvershoot = 0.12;
    }

    public sealed class Runner
    {
        public int Id;
        public int Column;
        public int SourceRow;
        public string Color;
        public int HoleId;
        public int Variant;
        public int EntryLane;
        public bool Finale;
        public bool Tripped;
        /// <summary>Được nam châm hút: bay thẳng tới hố, không cần lối trống trong tranh.</summary>
        public bool Lifted;
        internal bool TripRolled;
        public double DispatchedAt;
        public double StartDelay;
        public double RunDistance;
        public double PreAir;
        public double AirDuration;
        public double AirStartAt;
        public double AirEndAt;
        public double Duration;
        public double Elapsed;
        /// <summary>Đường đi thế giới từ ô xuất phát tới mép hố, có điểm vòng qua chỗ bị chắn khi cần.</summary>
        public readonly List<Vec2> Route = new List<Vec2>();
        public Vec2 Entry;
        public Vec2 Target;

        public double Progress => Duration > 0 ? Elapsed / Duration : 1;
    }

    /// <summary>RNG có seed (xorshift) để replay ổn định.</summary>
    public sealed class SeededRandom
    {
        uint _s;
        public SeededRandom(int seed) { _s = (uint)seed * 2654435761u ^ 0x9E3779B9u; if (_s == 0) _s = 1; }
        public double NextDouble()
        {
            _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5;
            return (_s >> 8) / 16777216.0;
        }
        public int Next(int max) => (int)(NextDouble() * max);
    }

    /// <summary>Túi kiểu nhảy: mọi kiểu xuất hiện trước khi lặp lại, không trùng hai lần liên tiếp.</summary>
    public sealed class DiveBag
    {
        readonly List<int> _bag = new List<int>();
        readonly int _count;
        int _last = -1;
        public DiveBag(int count) { _count = count; }

        public int Pick(SeededRandom rng)
        {
            if (_bag.Count == 0)
            {
                for (int i = 0; i < _count; i++) _bag.Add(i);
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
                }
                if (_bag.Count > 1 && _bag[_bag.Count - 1] == _last)
                {
                    int top = _bag[_bag.Count - 1];
                    _bag.RemoveAt(_bag.Count - 1);
                    _bag.Insert(0, top);
                }
            }
            _last = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            return _last;
        }
    }
}
