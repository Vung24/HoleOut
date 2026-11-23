using System;
using System.Collections.Generic;
using HoleOut.Core;
using TMPro;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Asset hình ảnh của Hole Out: prefab nhân vật/hố/khay/hiệu ứng và material theo mã màu tranh.
    /// Tạo bằng Tools/HoleOut/Build Visuals; đổi art chỉ cần đổi asset này.
    /// </summary>
    [CreateAssetMenu(menuName = "HoleOut/Visuals", fileName = "HoleOutVisuals")]
    public class VisualsSO : ScriptableObject
    {
        [Serializable]
        public class ColorVisual
        {
            public string code;
            public Color color = Color.white;
            [Tooltip("Thân nhân vật khi đứng/chạy; cũng dùng cho khối trên bàn và cổng.")]
            public Material agent;
            [Tooltip("Nhân vật đang ngồi trong tranh.")]
            public Material seated;
            public Material hole;
            public Material holeStencil;
            public List<Material> holeDoors = new List<Material>();
            public List<Material> holeCylinder = new List<Material>();
            [Tooltip("Thanh cổng nằm ngang (dọc theo cạnh trên/dưới). Trống thì dùng hộp màu.")]
            public Sprite gateBarX;
            [Tooltip("Thanh cổng nằm dọc (dọc theo cạnh trái/phải).")]
            public Sprite gateBarZ;
            [Tooltip("Mũi tên theo hướng thoát: lên, xuống, trái, phải (theo GateSide Top, Bottom, Left, Right).")]
            public Sprite arrowUp, arrowDown, arrowLeft, arrowRight;
            [Tooltip("Mảnh ghép thân khối trên bàn, theo thứ tự BoardBlockPart.")]
            public Sprite[] blockParts = new Sprite[0];
            [Tooltip("Nhân với sprite khối. Trắng nếu bộ sprite đã đúng màu; màu riêng khi mượn bộ sprite trắng.")]
            public Color blockTint = Color.white;
        }

        [Header("Prefab")]
        public AgentRig agentPrefab;
        public HoleRig holePrefab;
        public GameObject dockSlotPrefab;
        public GameObject holeCompleteEffect;
        [Tooltip("Bàn khối 6×4 dựng sẵn. Tạo bằng Tools/HoleOut/Build Level Prefabs.")]
        public BoardRig boardPrefab;
        public BlockRig blockPrefab;
        [Tooltip("Khối trên bàn có thân mesh, mỗi kích thước w×h một prefab. Tạo bằng Tools/HoleOut/Build Board Blocks.")]
        public List<BoardBlockRig> boardBlocks = new List<BoardBlockRig>();
        public BoardBlockRig lBoardBlock;
        public BoardBlockRig tBoardBlock;
        public BoardBlockRig plusBoardBlock;
        public TrayRig pictureTrayPrefab;
        public TrayRig dockTrayPrefab;

        [Header("Ô màu trong tranh")]
        public bool useSeatedMaterial;
        [Range(0.8f, 1.15f), Tooltip("Bề rộng ô màu so với ô lưới; trên 1 để các ô chồng nhẹ, lấp khe ở mép bo.")]
        public float pictureCellFill = 0.94f;
        [Range(0.7f, 1f), Tooltip("Chiều sâu ô nhân vật ngồi so với bề rộng, dùng để tạo khe giữa các hàng.")]
        public float pictureCellDepthRatio = 0.84f;
        [Range(0f, 40f), Tooltip("Góc ngả ô tranh về phía camera (độ) để lộ mặt trước khối.")]
        public float pictureCellTilt = 0f;
        [Range(0.1f, 1.2f), Tooltip("Chiều cao ô màu so với kích thước ô lưới.")]
        public float pictureCellHeight = 0.24f;
        [Range(0, 0.25f), Tooltip("Pha trắng nhẹ để màu tranh dịu hơn.")]
        public float pictureColorSoftness = 0.08f;

        [Header("Intro")]
        [Tooltip("Nhân vật chạy từ ngoài màn hình vào tranh khi bắt đầu màn.")]
        public IntroSettings intro = new IntroSettings();

        [Header("Khay chờ")]
        [Range(0.4f, 0.95f), Tooltip("Đường kính hố so với khoảng cách giữa các vị trí chờ.")]
        public float dockHoleFill = 0.68f;
        [Range(0.4f, 0.95f), Tooltip("Đường kính hốc nền so với khoảng cách giữa các vị trí chờ.")]
        public float dockSocketFill = 0.72f;
        [Min(0), Tooltip("Độ nhấc tối đa của hố trên đường bay vào khay chờ.")]
        public float dockFlightHeight = 0.9f;
        [Tooltip("Màu hốc trống cuối cùng ửng lên khi khay chỉ còn một chỗ.")]
        public Color dockWarnColor = new Color(0.55f, 0.14f, 0.2f);
        [Range(0, 1), Tooltip("Mức ửng màu cảnh báo tối đa. 0 = tắt.")]
        public float dockWarnStrength = 0.7f;
        [Min(0), Tooltip("Thời gian báo khay đầy khi khối chạm cổng: khối lắc ngang, viền khay chớp màu cảnh báo.")]
        public float dockFullAlertDuration = 0.4f;
        [Range(0, 0.3f), Tooltip("Biên độ lắc ngang của khối khi khay đầy. 0 = không lắc.")]
        public float dockFullShake = 0.1f;
        [Range(0, 0.4f), Tooltip("Biên độ nảy của hố mỗi khi nhận một nhân vật.")]
        public float holeCatchPunch = 0.1f;
        [Range(0, 24), Tooltip("Số hạt sáng bắn lên từ miệng hố mỗi khi nhận một nhân vật. 0 = tắt.")]
        public int holeCatchSparks = 5;
        [Min(0), Tooltip("Tốc độ bắn lên của hạt khi nhân vật lọt hố.")]
        public float holeCatchSparkSpeed = 2.2f;
        [Min(0), Tooltip("Kích thước hạt khi nhân vật lọt hố.")]
        public float holeCatchSparkSize = 0.14f;
        [Tooltip("Material hạt billboard khi nhân vật lọt hố; nhân màu đỉnh (màu hố), texture chấm tròn.")]
        public Material holeCatchSparkMaterial;

        [Header("Chữ")]
        public TMP_FontAsset font;

        [Header("Bàn")]
        public Material boardMaterial;
        public Material pictureMaterial;
        [Tooltip("Hiện khay lõm (đáy, mặt và viền) phía sau tranh.")]
        public bool showPictureTrayFill = true;
        [Tooltip("Mặt đất toon phía dưới toàn cảnh.")]
        public Material groundMaterial;
        [Tooltip("Màu nhân riêng cho mặt nền.")]
        public Color groundTint = Color.white;
        [Tooltip("Mặt khay tranh/bàn khối/khay chờ.")]
        public Material trayMaterial;
        [Tooltip("Viền khay.")]
        public Material rimMaterial;
        [Range(0, 0.6f), Tooltip("Độ sáng tối đa (pha trắng) của cổng cùng màu khi đang kéo khối. 0 = tắt.")]
        public float gateDragHighlight = 0.3f;
        [Tooltip("Thời gian khối nâng lên/hạ xuống khi bắt đầu/kết thúc kéo (giây); 0 = tức thì.")]
        [Range(0, 0.3f)] public float blockLiftDuration = 0.1f;
        [Tooltip("Độ nảy khi nhấc/đặt khối (vượt quá rồi về); 0 = không nảy.")]
        [Range(0, 3f)] public float blockLiftBounce = 1.6f;
        [Tooltip("Độ bám khi khối trượt về ô sau khi thả (càng lớn càng nhanh, không phụ thuộc FPS).")]
        [Range(5, 60)] public float blockSettleSharpness = 28f;
        [Tooltip("Ô nền bàn khối, lát mỗi ô một sprite. Trống thì chỉ dùng mặt khay.")]
        public Sprite boardCellSprite;

        [Header("UI")]
        [Tooltip("Sprite bo góc 9-slice cho panel và nút.")]
        public Sprite roundedSprite;
        public Color backgroundColor = new Color(0.62f, 0.8f, 0.9f);

        [Header("Màu")]
        public List<ColorVisual> colors = new List<ColorVisual>();

        public ColorVisual Get(string code)
        {
            foreach (var c in colors) if (c.code == code) return c;
            return colors.Count > 0 ? colors[0] : null;
        }

        /// <summary>Cùng một material cho ô tranh và nhân vật đứng, chạy, nhảy.</summary>
        public Material GetCharacterMaterial(string code)
        {
            var look = Get(code);
            return useSeatedMaterial && look?.seated ? look.seated : look?.agent;
        }

        public BoardBlockRig GetBoardBlock(int w, int h, BlockShape shape = BlockShape.Rectangle)
        {
            var frame = BlockData.FrameOf(shape);
            if (frame.HasValue)
            {
                if (frame.Value.w != w || frame.Value.h != h) return null;
                return shape == BlockShape.L ? lBoardBlock : shape == BlockShape.T ? tBoardBlock : plusBoardBlock;
            }
            foreach (var b in boardBlocks) if (b && b.size.x == w && b.size.y == h) return b;
            return null;
        }
    }
}
