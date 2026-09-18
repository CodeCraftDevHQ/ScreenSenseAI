using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ScreenTranslator.Services.Capture;

public sealed class GdiScreenCapture : IScreenCapture
{
    public Bitmap Capture(Rectangle area)
    {
        if (area.Width <= 0 || area.Height <= 0)
            throw new ArgumentException("Capture area is empty", nameof(area));

        var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);

        return bitmap;
    }

    public Rectangle GetActiveMonitorBounds()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        return screen.Bounds;
    }

    public Rectangle GetVirtualScreenBounds() => SystemInformation.VirtualScreen;
}
