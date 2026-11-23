using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    // Các nhóm cấu hình chỉ đọc theo nơi dùng, dựng từ VisualsSO.
    // Mỗi nhóm đọc thẳng từ asset nên giá trị chỉnh trên Inspector trong Play Mode vẫn áp dụng ngay.

    /// <summary>Tra màu theo mã, dùng chung cho mọi phần.</summary>
    public sealed class ColorPalette
    {
        readonly VisualsSO _source;

        public ColorPalette(VisualsSO source) => _source = source;

        public VisualsSO.ColorVisual Get(string code) => _source.Get(code);
        public Material CharacterMaterial(string code) => _source.GetCharacterMaterial(code);
        public Color ColorOf(string code) => _source.Get(code)?.color ?? Color.white;
    }

    /// <summary>Bàn khối, khối kéo và cổng.</summary>
    public sealed class BoardVisuals
    {
        readonly VisualsSO _source;

        public BoardVisuals(VisualsSO source, ColorPalette palette)
        {
            _source = source;
            Palette = palette;
        }

        public ColorPalette Palette { get; }
        public BoardRig BoardPrefab => _source.boardPrefab;
        public float GateDragHighlight => _source.gateDragHighlight;
        public float BlockLiftDuration => _source.blockLiftDuration;
        public float BlockLiftBounce => _source.blockLiftBounce;
        public float BlockSettleSharpness => _source.blockSettleSharpness;
        public float DockFullAlertDuration => _source.dockFullAlertDuration;
        public float DockFullShake => _source.dockFullShake;

        public BoardBlockRig BoardBlock(int w, int h, BlockShape shape) => _source.GetBoardBlock(w, h, shape);
    }

    /// <summary>Khay tranh, ô ngồi, nhân vật và cảnh chạy vào.</summary>
    public sealed class PictureVisuals
    {
        readonly VisualsSO _source;

        public PictureVisuals(VisualsSO source, ColorPalette palette)
        {
            _source = source;
            Palette = palette;
        }

        public ColorPalette Palette { get; }
        public AgentRig AgentPrefab => _source.agentPrefab;
        public TrayRig TrayPrefab => _source.pictureTrayPrefab;
        public bool ShowTrayFill => _source.showPictureTrayFill;
        public float CellFill => _source.pictureCellFill;
        public float CellHeight => _source.pictureCellHeight;
        public float CellDepthRatio => _source.pictureCellDepthRatio;
        public float CellTilt => _source.pictureCellTilt;
        public IntroSettings Intro => _source.intro;
    }

    /// <summary>Khay chờ, hố trên khay và hiệu ứng khi hố nhận nhân vật.</summary>
    public sealed class DockVisuals
    {
        readonly VisualsSO _source;

        public DockVisuals(VisualsSO source, ColorPalette palette)
        {
            _source = source;
            Palette = palette;
        }

        public ColorPalette Palette { get; }
        public TrayRig TrayPrefab => _source.dockTrayPrefab ? _source.dockTrayPrefab : _source.pictureTrayPrefab;
        public GameObject SlotPrefab => _source.dockSlotPrefab;
        public HoleRig HolePrefab => _source.holePrefab;
        public GameObject HoleCompleteEffect => _source.holeCompleteEffect;
        public float HoleFill => _source.dockHoleFill;
        public float SocketFill => _source.dockSocketFill;
        public float FlightHeight => _source.dockFlightHeight;
        public Color WarnColor => _source.dockWarnColor;
        public float WarnStrength => _source.dockWarnStrength;
        public float FullAlertDuration => _source.dockFullAlertDuration;
        public float CatchPunch => _source.holeCatchPunch;
        public int CatchSparks => _source.holeCatchSparks;
        public float CatchSparkSpeed => _source.holeCatchSparkSpeed;
        public float CatchSparkSize => _source.holeCatchSparkSize;
        public Material CatchSparkMaterial => _source.holeCatchSparkMaterial;
    }
}
