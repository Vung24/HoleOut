using System.Collections.Generic;
using HoleOut.Core;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>
    /// Prefab bàn khối 6×4 với tường liền giữa các cổng và trụ góc bo tròn.
    /// </summary>
    public class BoardRig : MonoBehaviour
    {
        /// <summary>Khe giữa mép ô và mặt trong tường.</summary>
        public const float WallGap = 0.03f;
        public const float WallThickness = 0.34f;
        /// <summary>Khoảng từ mép ô ngoài cùng tới mép ngoài khay, trùng mép ngoài tường.</summary>
        public const float Margin = WallGap + WallThickness;
        public const float WallTop = 0.30f;
        public const float GateTop = 0.10f;
        /// <summary>Góc kết thúc đúng tiếp tuyến tại mép ô ngoài cùng.</summary>
        public const float CornerRadius = Margin;

        [Tooltip("Cha của các khối kéo được trong màn.")]
        public Transform blocksRoot;
        [Tooltip("Bệ cổng theo thứ tự cạnh Top, Bottom, Left, Right; mỗi cạnh từ ô 0. Ô đầu cổng hiển thị cả cổng.")]
        public MeshFilter[] rim;
        [Tooltip("Renderer của bệ cổng, cùng thứ tự với rim.")]
        public MeshRenderer[] gateRenderers;
        [Tooltip("Mesh bệ cổng theo độ dài: phần tử i dài i + 1 ô.")]
        public Mesh[] gateMeshes;
        [Tooltip("Tường cùng thứ tự với bệ cổng. Tường ở ô đầu dải phủ các ô liền nhau tới cổng hoặc góc.")]
        public GameObject[] walls;
        Vector3[] _pos;
        MaterialPropertyBlock _gateProperties;
        // Cổng đang hiện: renderer, mã màu và màu gốc, để làm sáng cổng cùng màu khi kéo khối.
        readonly List<(MeshRenderer renderer, string code, Color color)> _shownGates = new List<(MeshRenderer, string, Color)>();
        string _litCode;
        float _litAmount;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Khoảng từ tâm ô mép tới đường tâm tường.</summary>
        public static float RimOffset => BoardSpace.BoardCell / 2 + WallGap + WallThickness / 2;
        public static bool IsHorizontal(GateSide side) => side == GateSide.Top || side == GateSide.Bottom;
        public static int SideLength(GateSide side) => IsHorizontal(side) ? DragSolver.BoardWidth : DragSolver.BoardHeight;

        void Cache()
        {
            if (_pos != null) return;
            _pos = new Vector3[rim.Length];
            for (int i = 0; i < rim.Length; i++) _pos[i] = rim[i].transform.localPosition;
        }

        /// <summary>Hiển thị một bệ có ký hiệu cho mỗi cổng, một đoạn tường cho mỗi dải ô sát mép còn trống.</summary>
        public void ApplyGates(IReadOnlyList<GateData> gates, ColorPalette palette)
        {
            Cache();
            _shownGates.Clear();
            _litCode = null;
            int sideStart = 0;
            for (int side = 0; side < 4; side++)
            {
                var gs = (GateSide)side;
                int count = SideLength(gs);
                int wallStart = -1;
                for (int i = 0; i < count; i++)
                {
                    int k = sideStart + i;
                    GateData gate = null;
                    foreach (var g in gates) if (g.side == gs && i >= g.start && i < g.start + g.len) gate = g;
                    walls[k].SetActive(false);
                    if (gate == null)
                    {
                        if (wallStart < 0) wallStart = i;
                    }
                    else if (wallStart >= 0)
                    {
                        ShowWall(sideStart, wallStart, i);
                        wallStart = -1;
                    }
                    bool head = gate != null && i == gate.start;
                    rim[k].gameObject.SetActive(head);
                    if (!head) continue;

                    int len = Mathf.Clamp(Mathf.Min(gate.len, count - i), 1, gateMeshes.Length);
                    var r = rim[k];
                    r.sharedMesh = gateMeshes[len - 1];
                    r.transform.localPosition = (_pos[k] + _pos[k + len - 1]) / 2;
                    _gateProperties ??= new MaterialPropertyBlock();
                    var gateColor = palette.ColorOf(gate.color);
                    _gateProperties.SetColor(BaseColorId, gateColor);
                    gateRenderers[k].SetPropertyBlock(_gateProperties, 1);
                    _shownGates.Add((gateRenderers[k], gate.color, gateColor));
                }
                if (wallStart >= 0) ShowWall(sideStart, wallStart, count);
                sideStart += count;
            }
        }

        /// <summary>
        /// Làm sáng các cổng mã <paramref name="code"/> theo <paramref name="amount"/> (0..1, pha trắng);
        /// code null hoặc amount 0 trả mọi cổng về màu gốc.
        /// </summary>
        public void SetGateHighlight(string code, float amount)
        {
            if (code == null) amount = 0;
            if (code == _litCode && Mathf.Abs(amount - _litAmount) < 0.004f) return;
            string previous = _litCode;
            _litCode = amount > 0 ? code : null;
            _litAmount = amount;
            _gateProperties ??= new MaterialPropertyBlock();
            foreach (var (renderer, gateCode, color) in _shownGates)
            {
                if (gateCode != code && gateCode != previous) continue;
                float k = gateCode == _litCode ? amount : 0;
                _gateProperties.SetColor(BaseColorId, Color.Lerp(color, Color.white, k));
                renderer.SetPropertyBlock(_gateProperties, 1);
            }
        }

        /// <summary>Thêm renderer của các cổng mã <paramref name="code"/> vào danh sách.</summary>
        public void CollectGateRenderers(string code, List<Renderer> into)
        {
            foreach (var (renderer, gateCode, _) in _shownGates)
                if (gateCode == code) into.Add(renderer);
        }

        void ShowWall(int sideStart, int start, int end)
        {
            var wall = walls[sideStart + start].transform;
            wall.localPosition = (_pos[sideStart + start] + _pos[sideStart + end - 1]) * 0.5f;
            wall.localScale = new Vector3(end - start, 1, 1);
            wall.gameObject.SetActive(true);
        }
    }
}
