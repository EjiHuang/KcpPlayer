using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using KcpPlayer.Avalonia.Services;
using KcpPlayer.Avalonia.Utils;
using Microsoft.Extensions.DependencyInjection;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using Ursa.Controls;

namespace KcpPlayer.Avalonia.Controls.OpenTkControl;

public class OpenTkPlayer : OpenGlControlBase, IOpenTkPlayer
{
    private ILogger? _logger;

    private AvaloniaTkContext? _avaloniaTkContext;
    private PixelSize _pixelSize;

    private readonly MediaService _mediaService;
    public int VideoWidth => _mediaService.VideoWidth;
    public int VideoHeight => _mediaService.VideoHeight;

    public OpenTkPlayer()
    {
        _mediaService = new MediaService();

        _logger = App.ServiceProvider?.GetRequiredService<ILogger>();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var pixelSize = GetPixelSize();
        if (pixelSize != _pixelSize)
        {
            _pixelSize = pixelSize;
            _mediaService.SetVideoSurfaceSize(pixelSize.Width, pixelSize.Height);
        }

        GL.Enable(EnableCap.DepthTest);
        GL.Enable(EnableCap.CullFace);

        GL.ClearColor(new OpenTK.Mathematics.Color4(0, 0, 0, 255));
        GL.Clear(
            ClearBufferMask.ColorBufferBit
                | ClearBufferMask.DepthBufferBit
                | ClearBufferMask.StencilBufferBit
        );

        _mediaService.SetVideoSurfaceSize(pixelSize.Width, pixelSize.Height);

        // 渲染视频帧
        _mediaService.RenderVideo();

        GL.Disable(EnableCap.DepthTest);

        Dispatcher.UIThread.InvokeAsync(RequestNextFrameRendering, DispatcherPriority.Background);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        // Initialize the OpenTK<->Avalonia Bridge
        _avaloniaTkContext = new AvaloniaTkContext(gl);
        GL.LoadBindings(_avaloniaTkContext);

        // 初始化渲染器
        _mediaService.InitializeVideoStreamRenderer();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
    }

    RTSPClient? _rtspClient;

    public void PlayRTSP(string url)
    {
        _rtspClient ??= new RTSPClient(_logger!);

        _rtspClient.NewVideoStream += (_, args) =>
        {
            switch (args.StreamType)
            {
                case "H264":
                    NewH264Stream(args, _rtspClient);
                    break;
                default:
                    _logger?.Warning("Unknow Video format {streamtype}", args.StreamType);
                    break;
            }
        };

        _rtspClient.SetupMessageCompleted += (_, _) =>
        {
            _rtspClient.Play();
        };

        _rtspClient.Connect(
            url,
            "admin",
            "admin",
            RTSPClient.RTP_TRANSPORT.TCP,
            RTSPClient.MEDIA_REQUEST.VIDEO_ONLY
        );
    }

    private void NewH264Stream(NewStreamEventArgs args, RTSPClient client)
    {
        _naluQueue.Clear();
        if (args.StreamConfigurationData is H264StreamConfigurationData h264StreamConfigurationData)
        {
            var sps = h264StreamConfigurationData.SPS;
            var pps = h264StreamConfigurationData.PPS;
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            var totalLength = 4 + sps.Length + 4 + pps.Length;
            var spsPps = new byte[totalLength];

            System.Buffer.BlockCopy(startCode, 0, spsPps, 0, 4);
            System.Buffer.BlockCopy(sps, 0, spsPps, 4, sps.Length);
            System.Buffer.BlockCopy(startCode, 0, spsPps, 4 + sps.Length, 4);
            System.Buffer.BlockCopy(pps, 0, spsPps, 4 + sps.Length + 4, pps.Length);

            _naluQueue.Enqueue(spsPps);

            //_stream.Write([0x00, 0x00, 0x00, 0x01]);
            //_stream.Write(h264StreamConfigurationData.SPS);

            //_stream.Write([0x00, 0x00, 0x00, 0x01]);
            //_stream.Write(h264StreamConfigurationData.PPS);
        }

        client.ReceivedVideoData += (_, dataArgs) =>
        {
            foreach (var nalUnitMem in dataArgs.Data)
            {
                var nalUnit = nalUnitMem.Span;
                // Output some H264 stream information
                if (nalUnit.Length > 5)
                {
                    int nal_ref_idc = (nalUnit[4] >> 5) & 0x03;
                    int nal_unit_type = nalUnit[4] & 0x1F;
                    string description = nal_unit_type switch
                    {
                        1 => "NON IDR NAL",
                        5 => "IDR NAL",
                        6 => "SEI NAL",
                        7 => "SPS NAL",
                        8 => "PPS NAL",
                        9 => "ACCESS UNIT DELIMITER NAL",
                        _ => "OTHER NAL",
                    };
                    _logger?.Information(
                        "NAL Ref = {nal_ref_idc} NAL Type = {nal_unit_type} {description}",
                        nal_ref_idc,
                        nal_unit_type,
                        description
                    );
                }

                _naluQueue.Enqueue(nalUnit.ToArray());
                //_stream.Write(nalUnit);
            }
        };
    }

    ConcurrentQueue<byte[]> _naluQueue = new();
    MemoryStream _stream = new(4096);

    public async Task<bool> PlayVideoAsync(string videoPath)
    {
        try
        {
            // 测试RTSP
            PlayRTSP(videoPath);

            await Task.Delay(1000);
            await _mediaService.DecodeFromQueueAsync(_naluQueue);
            //await _mediaService.DecodeFromStreamAsync(_stream);
            //await _mediaService.DecodeRTSPAsync(videoPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, ex.Message);
            await MessageBox.ShowAsync(ex.Message);
        }

        return false;
    }

    public async Task StopVideoAsync()
    {
        try
        {
            await _mediaService.StopVideoAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, ex.Message);
            await MessageBox.ShowAsync(ex.Message);
        }
    }

    private PixelSize GetPixelSize()
    {
        var scaling = TopLevel.GetTopLevel(this)!.RenderScaling;
        return new PixelSize(
            Math.Max(1, (int)(Bounds.Width * scaling)),
            Math.Max(1, (int)(Bounds.Height * scaling))
        );
    }

    #region properties

    public static readonly StyledProperty<string> VideoPathProperty = AvaloniaProperty.Register<
        OpenTkPlayer,
        string
    >(nameof(VideoPath), "");

    public string VideoPath
    {
        get => GetValue(VideoPathProperty);
        set => SetValue(VideoPathProperty, value);
    }

    #endregion
}
