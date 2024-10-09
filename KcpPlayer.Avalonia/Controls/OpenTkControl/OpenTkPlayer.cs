﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using KcpPlayer.Avalonia.Services;
using OpenTK.Graphics.OpenGL4;
using Serilog;
using System;
using System.Threading.Tasks;
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
        _mediaService.SetRenderCallback(() =>
        {
            // Schedule next UI update with avalonia
            //Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Background);
            Dispatcher.UIThread.InvokeAsync(RequestNextFrameRendering, DispatcherPriority.Background);
        });
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var pixelSize = GetPixelSize();
        if (pixelSize != _pixelSize)
        {
            _pixelSize = pixelSize;
            _mediaService.SetVideoSurfaceSize(pixelSize.Width, pixelSize.Height);
        }

        //_mediaService.SetVideoSurfaceSize(pixelSize.Width, pixelSize.Height);

        //GL.Viewport(0, 0, pixelSize.Width, pixelSize.Height);
        //GL.Clear(ClearBufferMask.ColorBufferBit);

        //GL.Enable(EnableCap.DepthTest);
        //GL.Enable(EnableCap.CullFace);

        //GL.ClearColor(new OpenTK.Mathematics.Color4(0, 32, 48, 255));
        //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        _mediaService.SetVideoSurfaceSize(pixelSize.Width, pixelSize.Height);

        // 渲染视频帧
        _mediaService.RenderVideo(pixelSize.Width, pixelSize.Height);

        //GL.Disable(EnableCap.DepthTest);


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

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<bool> PlayVideoAsync(string videoPath)
    {
        try
        {
            await _mediaService.DecodeRTSPAsync(videoPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, ex.Message);
            await MessageBox.ShowAsync(ex.Message);
        }

        return false;
    }

    private PixelSize GetPixelSize()
    {
        var scaling = TopLevel.GetTopLevel(this)!.RenderScaling;
        return new PixelSize(Math.Max(1, (int)(Bounds.Width * scaling)),
            Math.Max(1, (int)(Bounds.Height * scaling)));
    }

    #region properties

    public static readonly StyledProperty<string> VideoPathProperty =
        AvaloniaProperty.Register<OpenTkPlayer, string>(nameof(VideoPath), "");

    public string VideoPath
    {
        get => GetValue(VideoPathProperty);
        set => SetValue(VideoPathProperty, value);
    }

    #endregion
}