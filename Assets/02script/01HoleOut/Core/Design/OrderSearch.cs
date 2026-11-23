using System;
using System.Collections.Generic;
using System.Threading;

namespace HoleOut.Core
{
    public enum WinVerdict { Win, Lose, Unknown }

    /// <summary>
    /// Trả lời "từ trạng thái này còn thắng được không" bằng tìm kiếm sâu có ghi nhớ.
    /// Hết ngân sách thì trả <see cref="WinVerdict.Unknown"/> và không ghi nhớ kết quả đó.
    /// </summary>
    public sealed class WinOracle
    {
        readonly DrainModel _model;
        readonly Dictionary<DrainKey, bool> _memo = new Dictionary<DrainKey, bool>();
        readonly CancellationToken _cancel;
        int _left;

        public WinOracle(DrainModel model, CancellationToken cancel = default)
        {
            _model = model;
            _cancel = cancel;
        }

        public DrainModel Model => _model;

        public WinVerdict CanWin(DrainState s, int budget)
        {
            _left = budget;
            return Solve(s);
        }

        /// <summary>Kết quả đã biết chắc (không tốn ngân sách); null nếu chưa biết.</summary>
        public bool? Known(DrainState s)
        {
            if (s.Status == DrainStatus.Won) return true;
            if (s.Status == DrainStatus.Jammed) return false;
            return _memo.TryGetValue(_model.Key(s), out var v) ? v : (bool?)null;
        }

        WinVerdict Solve(DrainState s)
        {
            if (s.Status == DrainStatus.Won) return WinVerdict.Win;
            if (s.Status == DrainStatus.Jammed) return WinVerdict.Lose;
            var key = _model.Key(s);
            if (_memo.TryGetValue(key, out var known)) return known ? WinVerdict.Win : WinVerdict.Lose;
            if (--_left < 0) return WinVerdict.Unknown;
            if ((_left & 1023) == 0) _cancel.ThrowIfCancellationRequested();

            var choices = new List<int>();
            _model.Choices(s, choices);
            bool unknown = false;
            foreach (var b in choices)
            {
                var v = Solve(_model.Launch(s, b));
                if (v == WinVerdict.Win) { _memo[key] = true; return WinVerdict.Win; }
                if (v == WinVerdict.Unknown) unknown = true;
            }
            if (unknown) return WinVerdict.Unknown;
            _memo[key] = false;
            return WinVerdict.Lose;
        }
    }

    public struct OrderSearchResult
    {
        /// <summary>Thứ tự chỉ số khối; null nếu không tìm thấy.</summary>
        public List<int> Order;
        /// <summary>Đã dừng vì hết ngân sách: "không tìm thấy" chưa chắc là "không có".</summary>
        public bool Exhausted;
        public int Nodes;
    }

    /// <summary>Tìm thứ tự đưa khối ra thắng với mọi kiểu duyệt ô (lời giải "bền").</summary>
    public static class OrderSearch
    {
        public static readonly CellOrder[] AllOrders =
            { CellOrder.RowCol, CellOrder.RowColReversed, CellOrder.ColumnReversedRow, CellOrder.ColumnRow };

        /// <summary>Ngân sách oracle cho mỗi nút khi cắt nhánh chắc chắn thua.</summary>
        const int PruneBudget = 2000;

        /// <summary>Thứ tự thắng với cả bốn kiểu duyệt ô.</summary>
        public static bool IsRobust(LevelData level, IReadOnlyList<int> order, DrainModel[] models = null)
        {
            models = models ?? CreateModels(level);
            foreach (var m in models)
                if (m.Play(order).Status != DrainStatus.Won) return false;
            return true;
        }

        public static DrainModel[] CreateModels(LevelData level)
        {
            var models = new DrainModel[AllOrders.Length];
            for (int i = 0; i < models.Length; i++) models[i] = new DrainModel(level, AllOrders[i]);
            return models;
        }

        /// <param name="oracle">Oracle trên mô hình RowCol; dùng để cắt nhánh đã biết thua.</param>
        public static OrderSearchResult FindRobust(WinOracle oracle, DrainModel[] models, int nodeLimit, CancellationToken cancel = default)
        {
            var model = oracle.Model;
            var dead = new HashSet<DrainKey>();
            var path = new List<int>();
            var result = new OrderSearchResult();
            int nodes = 0;
            bool exhausted = false;

            bool Go(DrainState s)
            {
                if (s.Status == DrainStatus.Won) return IsRobust(model.Level, path, models);
                if (s.Status == DrainStatus.Jammed) return false;
                if (oracle.CanWin(s, PruneBudget) == WinVerdict.Lose) return false;
                var key = model.Key(s);
                if (dead.Contains(key)) return false;
                if (++nodes > nodeLimit) { exhausted = true; return false; }
                if ((nodes & 1023) == 0) cancel.ThrowIfCancellationRequested();

                var choices = new List<int>();
                model.Choices(s, choices);
                foreach (var b in choices)
                {
                    path.Add(b);
                    if (Go(model.Launch(s, b))) return true;
                    path.RemoveAt(path.Count - 1);
                    if (exhausted) return false;
                }
                dead.Add(key);
                return false;
            }

            if (Go(model.Initial())) result.Order = new List<int>(path);
            result.Exhausted = exhausted && result.Order == null;
            result.Nodes = nodes;
            return result;
        }
    }
}
