using System;
using System.Collections.Generic;
using UnityEngine;

namespace HoleOut.Runtime
{
    /// <summary>Ánh xạ mã màu một ký tự sang màu hiển thị. Thiếu asset thì dùng bộ màu chuẩn.</summary>
    [CreateAssetMenu(menuName = "HoleOut/Palette", fileName = "HoleOutPalette")]
    public class PaletteSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string code;
            public Color color;
        }

        public List<Entry> entries = new List<Entry>();

        /// <summary>
        /// Một màu trong bộ màu chuẩn: mặt trên ô tranh, vạch sáng, viền tối dưới và màu cho model 3D (nhân vật, hố).
        /// </summary>
        public readonly struct Swatch
        {
            public readonly string code, name;
            public readonly int id;
            public readonly uint body, highlight, edge, model;

            public Swatch(int id, string code, string name, uint body, uint highlight, uint edge, uint model)
            {
                this.id = id; this.code = code; this.name = name;
                this.body = body; this.highlight = highlight; this.edge = edge; this.model = model;
            }
        }

        /// <summary>Bộ màu chuẩn của tranh. M (cũng là ô bí ẩn) và E dùng chung màu Gray.</summary>
        public static readonly Swatch[] Standard =
        {
            new Swatch(0, "L", "Blue", 0x009EFF, 0x2AC8FF, 0x008AE4, 0x0073FF),
            new Swatch(1, "A", "Dark Blue", 0x004EED, 0x3879FF, 0x003AD4, 0x0E39CC),
            new Swatch(2, "D", "Black", 0x403F40, 0x686868, 0x2F2E30, 0x2F2F2F),
            new Swatch(3, "G", "Green", 0x74C927, 0x9AE55C, 0x52B827, 0x3FAC1D),
            new Swatch(4, "O", "Orange", 0xFF8000, 0xFFAE22, 0xF85C00, 0xFF6200),
            new Swatch(5, "B", "Pink", 0xF751D5, 0xFF87E4, 0xE83DB8, 0xC04983),
            new Swatch(6, "P", "Purple", 0x9F13E2, 0xBE58E7, 0x8F03C6, 0x7829E1),
            new Swatch(7, "R", "Red", 0xE50025, 0xFF5E7B, 0xB4001C, 0xA00D0D),
            new Swatch(8, "Q", "Turquoise", 0x00D4E1, 0x7EE6FA, 0x009DCD, 0x2FAACF),
            new Swatch(9, "Y", "Yellow", 0xF7D205, 0xFCEF8A, 0xF7AC05, 0xD2A626),
            new Swatch(10, "W", "White", 0xF1E6EC, 0xFAF6F8, 0xD8C8D7, 0xCCCCCC),
            new Swatch(11, "T", "Brown", 0x743310, 0xA86A4B, 0x5E260C, 0x613E23),
            new Swatch(12, "K", "Dark Green", 0x067F13, 0x16B426, 0x1C6303, 0x046000),
            new Swatch(13, "J", "Emerald", 0x0B6E66, 0x619989, 0x034E50, 0x1D6553),
            new Swatch(14, "I", "Lilac", 0xC983FB, 0xE8C0FE, 0xA858F2, 0xBF83E2),
            new Swatch(15, "S", "Sage", 0xA9C479, 0xCDDAB4, 0x7AA254, 0x959D67),
            new Swatch(16, "N", "Salmon", 0xD88A87, 0xD9A5A5, 0xCA6C66, 0xC37B7C),
            new Swatch(17, "V", "Lavender", 0x899CEE, 0xAEC6F4, 0x5E78E1, 0x878EC0),
            new Swatch(18, "F", "Terracotta", 0xB74F69, 0xC68C9B, 0xA13A52, 0x8D404D),
            new Swatch(19, "C", "Parmesan", 0xE4C891, 0xEADCAD, 0xD7B77E, 0xC4AA70),
            new Swatch(20, "H", "Brick", 0xC55A44, 0xD88574, 0xA64834, 0xB2544A),
            new Swatch(21, "M", "Gray", 0x9D9EA8, 0xC9C6C7, 0x787A8C, 0x888795),
            new Swatch(21, "E", "Gray", 0x9D9EA8, 0xC9C6C7, 0x787A8C, 0x888795),
            new Swatch(22, "X", "DarkGray", 0x5D5D5D, 0x929292, 0x4A4A4A, 0x474652),
            new Swatch(23, "U", "DarkPurple", 0x56259C, 0x8B51D4, 0x4B1482, 0x39205D),
            new Swatch(24, "Z", "GypsyPink", 0xD41481, 0xDC50AA, 0xA30961, 0xAA1D62),
            new Swatch(25, "1", "PowderPink", 0xEA9CDB, 0xF0C8EA, 0xE37ACE, 0xCF8AB2),
            new Swatch(26, "2", "PastelOrange", 0xF8A855, 0xFDCE94, 0xE58B33, 0xD2914F),
        };

        public static Color FromHex(uint hex) =>
            new Color(((hex >> 16) & 0xff) / 255f, ((hex >> 8) & 0xff) / 255f, (hex & 0xff) / 255f);

        public static bool TryGetSwatch(string code, out Swatch swatch)
        {
            foreach (var s in Standard) if (s.code == code) { swatch = s; return true; }
            swatch = default;
            return false;
        }

        public static bool TryGetSwatch(int id, out Swatch swatch)
        {
            foreach (var s in Standard) if (s.id == id) { swatch = s; return true; }
            swatch = default;
            return false;
        }

        public Color Get(string code)
        {
            foreach (var e in entries) if (e.code == code) return e.color;
            return Default(code);
        }

        public static Color Default(string code) =>
            TryGetSwatch(code, out var s) ? FromHex(s.body) : Color.magenta;
    }
}
