using FFmpeg.Wrapper;
using HPPH;
using ScreenCapture.NET;

namespace KcpLive;

internal class Program
{
    private static DX11ScreenCaptureService _screenCaptureService;
    private static IEnumerable<GraphicsCard> _graphicsCards;
    private static IEnumerable<Display> _displays;
    private static DX11ScreenCapture _screenCapture;

    private static CaptureZone<ColorBGRA> _fullscreen;
    private static CaptureZone<ColorBGRA> _topLeft;

    static void Main(string[] args)
    {
        _screenCaptureService = new DX11ScreenCaptureService();
        _graphicsCards = _screenCaptureService.GetGraphicsCards();
        _displays = _screenCaptureService.GetDisplays(_graphicsCards.First());
        _screenCapture = _screenCaptureService.GetScreenCapture(_displays.First());
        _screenCapture.Updated += OnScreenCaptureUpdated;

        _fullscreen = _screenCapture.RegisterCaptureZone(0, 0, _screenCapture.Display.Width, _screenCapture.Display.Height);
        _topLeft = _screenCapture.RegisterCaptureZone(0, 0, 100, 100, downscaleLevel: 1);

        _screenCapture.CaptureScreen();

        Console.ReadKey();
    }

    private static void OnScreenCaptureUpdated(object? sender, ScreenCaptureUpdatedEventArgs e)
    {
        if (e.IsSuccessful)
        {
            using (_topLeft.Lock())
            {
                unsafe
                {
                    RefImage<ColorBGRA> image = _topLeft.RefImage;
                    fixed (byte* ptr = &image.GetPinnableReference())
                    {
                        using VideoFrame frame = new VideoFrame();
                        frame.Data = &ptr;
                    }
                }
            }
        }
    }
}
