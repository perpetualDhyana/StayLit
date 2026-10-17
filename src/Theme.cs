using System.Drawing;

namespace StayLit
{
    internal enum TextTone { Ink, Muted, Highlight, HeroMuted }

    internal sealed class ThemePalette
    {
        public string Name, DisplayName;
        public bool IsDark;
        public Color Background, Surface, Raised, Ink, Muted, Accent, AccentText,
            Soft, Border, Hero, Highlight, DisabledFill, DisabledInk, Error;
    }

    internal static class Theme
    {
        public const string DefaultName = "WindowsLight";
        private static Color Hex(int value) { return Color.FromArgb(value >> 16, value >> 8 & 255, value & 255); }
        private static readonly ThemePalette windows = new ThemePalette {
            Name = "WindowsLight", DisplayName = "Windows 浅色", Background = Hex(0xF3F3F3),
            Surface = Hex(0xFFFFFF), Raised = Hex(0xF9F9F9), Ink = Hex(0x202020), Muted = Hex(0x5E5E5E),
            Accent = Hex(0x005FB8), AccentText = Hex(0x0057A8), Soft = Hex(0xE5F1FB), Border = Hex(0xD3D5D8),
            Hero = Hex(0xFFFFFF), Highlight = Hex(0x414141), DisabledFill = Hex(0xE9E9E9),
            DisabledInk = Hex(0x797979), Error = Hex(0xB3261E)
        };
        private static readonly ThemePalette paper = new ThemePalette {
            Name = "Paper", DisplayName = "纸白", Background = Hex(0xF7F6F3),
            Surface = Hex(0xFFFFFF), Raised = Hex(0xEFEEEB), Ink = Hex(0x292826), Muted = Hex(0x66635F),
            Accent = Hex(0x343330), AccentText = Hex(0x343330), Soft = Hex(0xEAE8E3), Border = Hex(0xDCD9D2),
            Hero = Hex(0xFFFFFF), Highlight = Hex(0x55524D), DisabledFill = Hex(0xEEECE8),
            DisabledInk = Hex(0x88847D), Error = Hex(0xA82822)
        };
        private static readonly ThemePalette graphite = new ThemePalette {
            Name = "Graphite", DisplayName = "石墨黑", IsDark = true, Background = Hex(0x111113),
            Surface = Hex(0x1B1B1F), Raised = Hex(0x232328), Ink = Hex(0xEDEDEF), Muted = Hex(0xA3A3AE),
            Accent = Hex(0x5B65CB), AccentText = Hex(0xB7BEFF), Soft = Hex(0x242436), Border = Hex(0x33333A),
            Hero = Hex(0x18181C), Highlight = Hex(0xC4C4CF), DisabledFill = Hex(0x1F1F23),
            DisabledInk = Hex(0x83838F), Error = Hex(0xFFA3A3)
        };
        private static ThemePalette current = windows;
        public static string Normalize(string name) { return name == "Graphite" || name == "Paper" ? name : DefaultName; }
        public static void Select(string name) { current = Normalize(name) == "Graphite" ? graphite : name == "Paper" ? paper : windows; }
        public static string Name { get { return current.Name; } }
        public static string DisplayName { get { return current.DisplayName; } }
        public static bool IsDark { get { return current.IsDark; } }
        public static Color Background { get { return current.Background; } }
        public static Color Surface { get { return current.Surface; } }
        public static Color Raised { get { return current.Raised; } }
        public static Color Ink { get { return current.Ink; } }
        public static Color Muted { get { return current.Muted; } }
        public static Color Accent { get { return current.Accent; } }
        public static Color AccentText { get { return current.AccentText; } }
        public static Color Soft { get { return current.Soft; } }
        public static Color Border { get { return current.Border; } }
        public static Color Hero { get { return current.Hero; } }
        public static Color HeroMuted { get { return current.Muted; } }
        public static Color Highlight { get { return current.Highlight; } }
        public static Color DisabledFill { get { return current.DisabledFill; } }
        public static Color DisabledInk { get { return current.DisabledInk; } }
        public static Color Error { get { return current.Error; } }
        public static Color TextColor(TextTone tone)
        {
            return tone == TextTone.Muted ? Muted : tone == TextTone.Highlight ? Highlight :
                tone == TextTone.HeroMuted ? HeroMuted : Ink;
        }
        public static Color Hover(Color color, bool pressed)
        {
            double fraction = IsDark ? (pressed ? 0.045 : 0.025) : (pressed ? 0.08 : 0.04);
            int target = IsDark ? 255 : 0;
            return Color.FromArgb(color.R + (int)((target - color.R) * fraction),
                color.G + (int)((target - color.G) * fraction), color.B + (int)((target - color.B) * fraction));
        }
    }

    internal sealed class ThemedLabel : System.Windows.Forms.Label
    {
        public TextTone Tone;
    }
}
