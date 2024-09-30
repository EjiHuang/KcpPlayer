﻿using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using KcpPlayer.Avalonia.Services;
using KcpPlayer.Avalonia.Utils;
using Serilog;
using Ursa.Controls;

namespace KcpPlayer.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly IMediaService _mediaService;

    public ObservableCollection<string> Urls { get; set; } = [];

    #region properties

    public int VideoWidth => _mediaService.VideoWidth;
    public int VideoHeight => _mediaService.VideoHeight;

    private string _url = "rtsp://127.0.0.1:8554/live/0";
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    #endregion

    public MainWindowViewModel(ILogger logger, IMediaService mediaService)
    {
        _logger = logger;
        _mediaService = mediaService;

        // 初始化数据绑定
        Urls.Add("rtsp://127.0.0.1:8554/live/0");
        Urls.Add("rtsp://rtspstream:f653638d5e1d579e7ba0aaf97e9e54ac@zephyr.rtsp.stream/movie");
        Urls.Add("rtsp://rtspstream:ab0fed99d825e52d589af4e91a1842d0@zephyr.rtsp.stream/pattern");

        // 添加获取到的摄像头设备
        var cameraDevices = FFmpegCameraManager.GetCameraDevices();
        if (cameraDevices != null)
        {
            foreach (var camera in cameraDevices)
            {
                Urls.Add(camera.Path);
            }
        }
    }

    /// <summary>
    /// 初始化渲染器
    /// </summary>
    public void InitialiazeRenderer()
    {
        // 初始化渲染器
        _mediaService.InitializeVideoStreamRenderer();
    }

    /// <summary>
    /// 渲染视频帧
    /// </summary>
    public void VideoRender()
    {
        _mediaService.Render();
    }

    [RelayCommand]
    private async Task PlayVideoAsync()
    {
        try
        {
            if (Url.StartsWith("kcp://", StringComparison.InvariantCultureIgnoreCase))
            {
                await _mediaService.DecodeRTSPAsync("udp://127.0.0.1:40002");
            }
            else
            {
                await _mediaService.DecodeRTSPAsync(Url);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, ex.Message);
            await MessageBox.ShowAsync(ex.Message);
        }
    }

    [RelayCommand]
    private async Task StopVideoAsync()
    {
        await _mediaService.StopVideoAsync();
    }
}
