using UnityEngine;

namespace FillItUp
{
    /// <summary>Every colour in the game, in one place.</summary>
    public static class Palette
    {
        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        // UI
        public static readonly Color Ink = Hex(0x1E3A5F);
        public static readonly Color InkSoft = Hex(0x47658A);
        public static readonly Color Paper = Hex(0xFFFDF6);
        public static readonly Color PaperBlue = Hex(0xE8F7FF);
        public static readonly Color UiShadow = Hex(0x0E2742, 0.28f);
        public static readonly Color Gold = Hex(0xFFC93C);
        public static readonly Color GoldDark = Hex(0xE09A12);
        public static readonly Color Correct = Hex(0x3DD68C);
        public static readonly Color Wrong = Hex(0xFF6B6B);
        public static readonly Color Highlight = Hex(0xFF8A00);

        // buttons (face, lip)
        public static readonly Color Green = Hex(0x4CD964), GreenLip = Hex(0x2E9E48);
        public static readonly Color Blue = Hex(0x36A9F7), BlueLip = Hex(0x1C76C2);
        public static readonly Color Orange = Hex(0xFF9F2E), OrangeLip = Hex(0xD06F0A);
        public static readonly Color Pink = Hex(0xFF6FA8), PinkLip = Hex(0xC9437A);
        public static readonly Color Purple = Hex(0x9B6BFF), PurpleLip = Hex(0x6A41C9);
        public static readonly Color Teal = Hex(0x26C6C6), TealLip = Hex(0x13918F);
        public static readonly Color Grey = Hex(0xB9C6D3), GreyLip = Hex(0x8396AA);

        // level theme colours
        public static readonly Color[] LevelColors = { Hex(0x36A9F7), Hex(0xFF9F2E), Hex(0x9B6BFF) };
        public static readonly Color[] LevelLips = { Hex(0x1C76C2), Hex(0xD06F0A), Hex(0x6A41C9) };

        // world
        public static readonly Color SkyTop = Hex(0x5EC8FF);
        public static readonly Color SkyBottom = Hex(0xD9F4FF);
        public static readonly Color Water = Hex(0x3FB6FF, 0.78f);
        public static readonly Color MascotBody = Hex(0x55C8FF);
        public static readonly Color PedestalIdle = Hex(0xFFF3D6);
        public static readonly Color PedestalHover = Hex(0xFFE08A);
        public static readonly Color PedestalRight = Hex(0x7CF0B0);
        public static readonly Color PedestalWrong = Hex(0xFFB3B3);
        public static readonly Color PedestalOff = Hex(0xA9B4C2);

        public static readonly Color[] Confetti =
        {
            Hex(0xFF6FA8), Hex(0xFFC93C), Hex(0x4CD964), Hex(0x36A9F7), Hex(0x9B6BFF), Hex(0xFF9F2E)
        };
    }
}
