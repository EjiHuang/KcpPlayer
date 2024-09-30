using KcpPlayer.Avalonia.Services;
using KcpPlayer.Avalonia.Utils;
using Serilog;
using System.Collections.ObjectModel;

namespace KcpPlayer.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private readonly IMediaService _mediaService;

    public ObservableCollection<string> Urls { get; set; } = [];

    public MainWindowViewModel(ILogger logger, IMediaService mediaService)
    {
        _logger = logger;
        _mediaService = mediaService;

        // 初始化数据绑定
        Urls.Add("rtsp://rtspstream:f653638d5e1d579e7ba0aaf97e9e54ac@zephyr.rtsp.stream/movie");
        Urls.Add(
            "rtsp://rtspstream:ab0fed99d825e52d589af4e91a1842d0@zephyr.rtsp.stream/pattern"
        );

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
}
