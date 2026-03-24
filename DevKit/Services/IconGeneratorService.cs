using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevKit.Services
{
    public static class IconGeneratorService
    {
        public static BitmapSource CreateEditorIcon(int size = 32) => Render(size, (dc, s) =>
        {
            double mid = s / 2.0;
            dc.DrawEllipse(B(0x7A, 0xA2, 0xF7), null, new Point(mid, mid), mid - 1, mid - 1);
            var t = Txt("</>", s * 0.35, Brushes.White, true);
            dc.DrawText(t, new Point(mid - t.Width / 2, mid - t.Height / 2));
        });

        public static BitmapSource CreateScriptsIcon(int size = 32) => Render(size, (dc, s) =>
        {
            double mid = s / 2.0;
            dc.DrawEllipse(B(0x7A, 0xA2, 0xF7), null, new Point(mid, mid), mid - 1, mid - 1);
            var pen = new Pen(Brushes.White, s * 0.07) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            for (double y = 0.33; y <= 0.67; y += 0.17)
            {
                dc.DrawLine(pen, new Point(s * 0.28, s * y), new Point(s * 0.72, s * y));
                dc.DrawEllipse(Brushes.White, null, new Point(s * 0.22, s * y), s * 0.035, s * 0.035);
            }
        });

        public static BitmapSource CreateScriptButtonIcon(string label, int size = 32)
        {
            var colors = new[] { (0x7A,0xA2,0xF7),(0xA6,0xE3,0xA1),(0xF9,0xE2,0xAF),(0xCB,0xA6,0xF7),(0x94,0xE2,0xD5),(0xFA,0xB3,0x87),(0xF3,0x8B,0xA8) };
            var (r, g, b) = colors[Math.Abs(label.GetHashCode()) % colors.Length];
            string initials = label.Length >= 2 ? label.Substring(0, 2).ToUpper() : label.ToUpper();
            return Render(size, (dc, s) =>
            {
                double mid = s / 2.0;
                dc.DrawRoundedRectangle(B((byte)r, (byte)g, (byte)b), null, new Rect(1, 1, s - 2, s - 2), s * 0.2, s * 0.2);
                var t = Txt(initials, s * 0.38, B(0x1E, 0x1E, 0x2E), true);
                dc.DrawText(t, new Point(mid - t.Width / 2, mid - t.Height / 2));
            });
        }

        private static BitmapSource Render(int size, Action<DrawingContext, double> draw)
        { var v = new DrawingVisual(); using (var dc = v.RenderOpen()) draw(dc, size); var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32); bmp.Render(v); bmp.Freeze(); return bmp; }

        private static SolidColorBrush B(byte r, byte g, byte b) => new SolidColorBrush(Color.FromRgb(r, g, b));
        private static FormattedText Txt(string text, double size, Brush brush, bool bold) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Consolas"), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, brush, 1.0);
    }
}
