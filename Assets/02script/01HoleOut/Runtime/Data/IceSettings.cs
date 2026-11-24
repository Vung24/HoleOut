using System;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Thông số hình ảnh của vỏ băng trên khối đóng băng. Luật băng nằm ở <see cref="Core.BlockBoard.ThawAfterExit"/>.</summary>
    [Serializable]
    public class IceSettings
    {
        [Tooltip("Material dùng shader HoleOut/Ice. Trống thì khối băng không có vỏ (luật vẫn chạy).")]
        public Material material;
        [Range(0f, 0.1f), Tooltip("Vỏ băng rộng hơn thân khối mỗi phía (đơn vị ô).")]
        public float margin = 0.035f;
        [Range(0f, 0.2f), Tooltip("Mặt trên vỏ băng cao hơn vành hố (đơn vị ô).")]
        public float topClearance = 0.05f;
        [Range(0f, 1f), Tooltip("Độ nứt tối đa ngay trước khi vỡ; nứt tăng đều theo số lớp đã mất.")]
        public float maxCrack = 0.85f;

        [Header("Nứt")]
        [Range(0.05f, 0.6f), Tooltip("Thời gian chớp sáng và nảy khi mất một lớp (giây).")]
        public float crackDuration = 0.28f;
        [Range(1f, 2f), Tooltip("Số trên khối phồng tới tỉ lệ này rồi về.")]
        public float countPunch = 1.45f;
        [Range(1f, 1.15f), Tooltip("Vỏ băng phồng nhẹ tới tỉ lệ này rồi về.")]
        public float shellPunch = 1.05f;

        [Header("Vỡ")]
        [Range(0, 24)] public int shardCount = 10;
        [Range(0.2f, 1.2f), Tooltip("Thời gian mảnh băng bay và tan (giây).")]
        public float shatterDuration = 0.55f;
        [Range(0.5f, 6f), Tooltip("Tốc độ văng ra của mảnh băng (ô/giây).")]
        public float shardSpeed = 2.4f;
        [Range(0.05f, 0.4f), Tooltip("Cỡ mảnh băng (đơn vị ô).")]
        public float shardSize = 0.16f;
        [Min(0f), Tooltip("Gia tốc rơi của mảnh băng (ô/giây²).")]
        public float shardGravity = 9f;
    }
}
