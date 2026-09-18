using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenTranslator.Services;

public static class ColorSampler
{
    public static (System.Windows.Media.Color Background, System.Windows.Media.Color Foreground) Sample(Bitmap bitmap, Rectangle area)
    {
        area = Rectangle.Intersect(area, new Rectangle(0, 0, bitmap.Width, bitmap.Height));

        if (area.Width <= 0 || area.Height <= 0)
            return (System.Windows.Media.Colors.Black, System.Windows.Media.Colors.White);

        var data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        var histogram = new Dictionary<int, int>();
        var bytes = new byte[data.Stride * data.Height];
        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
        bitmap.UnlockBits(data);

        for (var y = 0; y < data.Height; y++)
        {
            for (var x = 0; x < data.Width; x++)
            {
                var offset = y * data.Stride + x * 4;
                var b = bytes[offset];
                var g = bytes[offset + 1];
                var r = bytes[offset + 2];

                var quantized = ((r / 16) << 8) | ((g / 16) << 4) | (b / 16);
                histogram[quantized] = histogram.GetValueOrDefault(quantized) + 1;
            }
        }

        if (histogram.Count == 0)
            return (System.Windows.Media.Colors.Black, System.Windows.Media.Colors.White);

        var ranked = histogram.OrderByDescending(x => x.Value).ToList();

        var background = Unquantize(ranked[0].Key);
        var foreground = ranked.Count > 1 ? Unquantize(ranked[1].Key) : Invert(background);

        if (ContrastRatio(background, foreground) < 3.0)
            foreground = Luminance(background) > 0.5 ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White;

        return (background, foreground);
    }

    private static System.Windows.Media.Color Unquantize(int key)
    {
        var r = (byte)(((key >> 8) & 0xF) * 16 + 8);
        var g = (byte)(((key >> 4) & 0xF) * 16 + 8);
        var b = (byte)((key & 0xF) * 16 + 8);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    private static System.Windows.Media.Color Invert(System.Windows.Media.Color color)
        => System.Windows.Media.Color.FromRgb((byte)(255 - color.R), (byte)(255 - color.G), (byte)(255 - color.B));

    private static double Luminance(System.Windows.Media.Color color)
        => (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;

    private static double ContrastRatio(System.Windows.Media.Color a, System.Windows.Media.Color b)
    {
        var la = Luminance(a) + 0.05;
        var lb = Luminance(b) + 0.05;
        return la > lb ? la / lb : lb / la;
    }
}
