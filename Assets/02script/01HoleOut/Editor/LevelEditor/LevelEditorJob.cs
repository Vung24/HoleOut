using System;
using System.Threading;
using System.Threading.Tasks;
using HoleOut.Core;
using UnityEditor;
using UnityEngine;

namespace HoleOut.EditorTools
{
    /// <summary>Một việc tính toán chạy ở luồng nền (chỉ dùng HoleOut.Core), có Hủy và báo khi xong trên luồng chính.</summary>
    sealed class LevelEditorJob<T>
    {
        Task<T> _task;
        CancellationTokenSource _cts;
        Action<T> _done;
        Action<Exception> _failed;
        double _started;

        public bool Running => _task != null;
        public string Label { get; private set; }
        public double Elapsed => Running ? EditorApplication.timeSinceStartup - _started : 0;

        public void Start(string label, Func<CancellationToken, T> work, Action<T> done, Action<Exception> failed = null)
        {
            Cancel();
            Label = label;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _done = done;
            _failed = failed;
            _started = EditorApplication.timeSinceStartup;
            _task = Task.Run(() => work(token), token);
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _task = null;
            _cts = null;
        }

        /// <summary>Gọi định kỳ trên luồng chính. Trả về true khi vừa xong (để vẽ lại).</summary>
        public bool Poll()
        {
            if (_task == null || !_task.IsCompleted) return false;
            var t = _task;
            _task = null;
            _cts = null;
            if (t.IsCanceled || t.Exception?.InnerException is OperationCanceledException) return true;
            if (t.IsFaulted)
            {
                if (_failed != null) _failed(t.Exception.InnerException);
                else Debug.LogException(t.Exception.InnerException);
                return true;
            }
            _done?.Invoke(t.Result);
            return true;
        }

        /// <summary>Bản sao sâu dữ liệu màn để luồng nền không chạm vào asset đang sửa.</summary>
        public static LevelData Snapshot(LevelData data) => JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(data));

        public static LevelSolution Snapshot(LevelSolution s) => s == null ? null : JsonUtility.FromJson<LevelSolution>(JsonUtility.ToJson(s));
    }
}
