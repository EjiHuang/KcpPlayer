using FFmpeg.AutoGen;
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

    private static SwScaler _scaler;
    private static VideoEncoder _videoEncoder;
    private static MediaMuxer _mediaMuxer;
    private static MediaStream _videoStream;
    private static Rational _frameRate = new(24, 1);

    private static bool _isRecording = false;

    static void Main(string[] args)
    {
        ffmpeg.RootPath = AppContext.BaseDirectory + "runtimes/win-x64/native";
        ffmpeg.av_log_set_level(ffmpeg.AV_LOG_DEBUG);

        _screenCaptureService = new DX11ScreenCaptureService();
        _graphicsCards = _screenCaptureService.GetGraphicsCards();
        _displays = _screenCaptureService.GetDisplays(_graphicsCards.First());
        _screenCapture = _screenCaptureService.GetScreenCapture(_displays.First());

        _fullscreen = _screenCapture.RegisterCaptureZone(
            0,
            0,
            _screenCapture.Display.Width,
            _screenCapture.Display.Height
        );
        _topLeft = _screenCapture.RegisterCaptureZone(0, 0, 100, 100, downscaleLevel: 1);

        Console.WriteLine("[1] Capture and record.");
        Console.WriteLine("[2] Capture and send.");

        start:
        Console.Write("Select number: ");

        if (Console.ReadLine() == "1")
        {
            DoCaptureAndRecord();
        }
        else
        {
            goto start;
        }

        Console.ReadKey();
    }

    private static void DoCaptureAndRecord()
    {
        Console.WriteLine("Screen capturing and recording...");
        Console.WriteLine("Press ESC to stop.");
        var frameNo = 0;

        while (true)
        {
            _screenCapture.CaptureScreen();
            using (_fullscreen.Lock())
            {
                unsafe
                {
                    RefImage<ColorBGRA> image = _fullscreen.RefImage;
                    fixed (byte* ptr = &image.GetPinnableReference())
                    {
                        AVFrame* frame = ffmpeg.av_frame_alloc();
                        frame->width = image.Width;
                        frame->height = image.Height;
                        frame->format = (int)AVPixelFormat.AV_PIX_FMT_BGRA;
                        frame->data[0] = ptr;
                        frame->linesize[0] = image.RawStride;
                        using var rgbFrame = new VideoFrame(frame, true);
                        using var outFrame = new VideoFrame(
                            image.Width,
                            image.Height,
                            PixelFormats.YUV420P
                        );

                        _scaler ??= new SwScaler(rgbFrame.Format, outFrame.Format);
                        _scaler.Convert(rgbFrame, outFrame);

                        if (_videoEncoder == null)
                        {
                            _videoEncoder = new VideoEncoder(
                                MediaCodec.GetEncoder("libx264"),
                                outFrame.Format,
                                _frameRate
                            );
                            _videoEncoder.SetOption("crf", "24");
                            _videoEncoder.SetOption("preset", "faster");
                        }

                        if (_mediaMuxer == null)
                        {
                            var fileName = "_cap.mp4";
                            var muxerOpts = new List<KeyValuePair<string, string>>();
                            if (fileName.EndsWith(".mp4"))
                            {
                                muxerOpts.Add(new("movflags", "+faststart"));
                            }

                            _mediaMuxer = new MediaMuxer(fileName);
                            _videoStream = _mediaMuxer.AddStream(_videoEncoder);
                            _mediaMuxer.Open(muxerOpts);

                            //outFrame.Save("_demo.jpg");
                        }

                        outFrame.PresentationTimestamp = _videoEncoder.GetFramePts(frameNo);
                        _mediaMuxer.EncodeAndWrite(_videoStream, _videoEncoder, outFrame);

                        frameNo++;

                        // 只录制10秒
                        //if (frameNo > 240)
                        //{
                        //    _mediaMuxer.EncodeAndWrite(_videoStream, _videoEncoder, null);
                        //    Console.WriteLine("Record finish.");
                        //    break;
                        //}

                        if (Console.KeyAvailable)
                        {
                            var key = Console.ReadKey(intercept: true);
                            if (key.Key == ConsoleKey.Escape)
                            {
                                _mediaMuxer.EncodeAndWrite(_videoStream, _videoEncoder, null);
                                Console.WriteLine("Record finish.");
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
