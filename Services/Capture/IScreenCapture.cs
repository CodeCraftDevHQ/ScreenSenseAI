using System.Drawing;

namespace ScreenTranslator.Services.Capture;

public interface IScreenCapture
{
    Bitmap Capture(Rectangle area);
    Rectangle GetActiveMonitorBounds();
    Rectangle GetVirtualScreenBounds();
}
