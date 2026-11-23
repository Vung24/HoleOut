using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Màn HoleOut: dữ liệu đầu vào bất biến, lời giải đã kiểm tra và metadata trình bày.</summary>
    [CreateAssetMenu(menuName = "HoleOut/Level", fileName = "HoleOutLevel")]
    public class LevelSO : ScriptableObject
    {
        [Tooltip("Tên hiển thị.")]
        public string displayName;

        [Tooltip("Nguồn nhập, ví dụ campaign:4. Chỉ dùng để truy vết.")]
        public string source;

        [Tooltip("Bộ màu riêng của màn; để trống dùng bộ màu trên controller.")]
        public VisualsSO visuals;

        public LevelData data = new LevelData();

        [Tooltip("Tỉ lệ nhân vật; 0 = mặc định theo số cột.")]
        public float charScale;

        [Header("Hướng dẫn")]
        public bool tutorialFirstDrag;

        [Header("Lời giải")]
        public LevelSolution solution = new LevelSolution();

        [Tooltip("Kết quả replay gần nhất trong Editor.")]
        public string lastReplay;

        [Header("Thiết kế")]
        [Tooltip("Tên cấp mục tiêu trong bộ cấp độ khó (HoleOutDifficultyProfileSO).")]
        public string targetTier;

        [Tooltip("Thước đo độ khó gần nhất từ Level Editor.")]
        public DifficultyReport lastReport;

        [Tooltip("Phiên bản dữ liệu lúc phân tích; khác phiên bản hiện tại nghĩa là kết quả đã cũ.")]
        public int lastReportVersion;
    }
}
