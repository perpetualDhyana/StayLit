using System.Drawing;
using System.Drawing.Drawing2D;

namespace StayLit
{
    internal static class Branding
    {
        public static readonly Color Accent = Color.FromArgb(91, 101, 203);
        public static Bitmap DrawIcon(int size)
        {
            return DrawIcon(size, Accent);
        }
        public static Bitmap DrawIcon(int size, Color accent)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size / 64f, size / 64f);
                using (GraphicsPath path = Rounded(new RectangleF(1, 1, 62, 62), 16))
                using (Brush background = new SolidBrush(accent))
                    g.FillPath(background, path);
                using (Pen light = new Pen(Color.White, 3.5f))
                {
                    light.StartCap = LineCap.Round;
                    light.EndCap = LineCap.Round;
                    g.DrawEllipse(light, 21, 21, 22, 22);
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * System.Math.PI / 4;
                        g.DrawLine(light, 32 + (float)System.Math.Cos(angle) * 18,
                            32 + (float)System.Math.Sin(angle) * 18,
                            32 + (float)System.Math.Cos(angle) * 23,
                            32 + (float)System.Math.Sin(angle) * 23);
                    }
                }
            }
            return bitmap;
        }

        public static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
