using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StayLit
{
    internal static class NativeAppearance
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public static void Apply(IntPtr window)
        {
            // Window-local attributes only. Windows versions that do not
            // support them keep their standard native window frame.
            int dark = Theme.IsDark ? 1 : 0;
            DwmSetWindowAttribute(window, 20, ref dark, sizeof(int));
            SetColor(window, 34, Theme.Border);
            SetColor(window, 35, Theme.Background);
            SetColor(window, 36, Theme.Ink);
        }

        private static void SetColor(IntPtr window, int attribute, Color color)
        {
            int colorRef = color.R | color.G << 8 | color.B << 16;
            DwmSetWindowAttribute(window, attribute, ref colorRef, sizeof(int));
        }
    }

    internal sealed class AppMenuRenderer : ToolStripProfessionalRenderer
    {
        public AppMenuRenderer() : base(new AppMenuColors()) { }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            float scale = e.Graphics.DpiX / 96f;
            float x = e.ImageRectangle.Left + e.ImageRectangle.Width / 2f;
            float y = e.ImageRectangle.Top + e.ImageRectangle.Height / 2f;
            System.Drawing.Drawing2D.SmoothingMode original = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen pen = new Pen(Theme.Ink, 1.6f * scale))
                e.Graphics.DrawLines(pen, new PointF[] { new PointF(x - 4 * scale, y),
                    new PointF(x - scale, y + 3 * scale), new PointF(x + 5 * scale, y - 4 * scale) });
            e.Graphics.SmoothingMode = original;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Enabled ? Theme.Ink : Theme.Muted;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.Ink : Theme.Muted;
            base.OnRenderItemText(e);
        }
    }

    internal sealed class AppMenuColors : ProfessionalColorTable
    {
        public AppMenuColors() { UseSystemColors = false; }
        public override Color ToolStripDropDownBackground { get { return Theme.Surface; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Surface; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Surface; } }
        public override Color MenuItemSelected { get { return Theme.Soft; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Soft; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Soft; } }
        public override Color MenuItemPressedGradientBegin { get { return Theme.Soft; } }
        public override Color MenuItemPressedGradientMiddle { get { return Theme.Soft; } }
        public override Color MenuItemPressedGradientEnd { get { return Theme.Soft; } }
        public override Color MenuItemBorder { get { return Theme.Border; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Border; } }
        public override Color CheckBackground { get { return Theme.Soft; } }
        public override Color CheckSelectedBackground { get { return Theme.Soft; } }
        public override Color CheckPressedBackground { get { return Theme.Soft; } }
    }
}
