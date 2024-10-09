using CommunityToolkit.Mvvm.Input;
using KcpPlayer.Avalonia.Controls.OpenTkControl;
using KcpPlayer.Avalonia.Utils;
using Serilog;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KcpPlayer.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger _logger;
    private IOpenTkPlayer? _player;

    public ObservableCollection<string> Urls { get; set; } = [];

    #region properties

    private string _url = "rtsp://127.0.0.1:8554/live/0";
    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    #endregion

    public MainWindowViewModel(ILogger logger/*, IMediaService mediaService*/)
    {
        _logger = logger;

        // 初始化数据绑定
        Urls.Add("rtsp://192.168.0.116:8554/cam");
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

    public void SetOpenTkPlayer(IOpenTkPlayer player)
    {
        _player = player;
    }

    [RelayCommand]
    private async Task PlayVideoAsync()
    {
        var result = await _player!.PlayVideoAsync(Url);
    }

    [RelayCommand]
    private async Task StopVideoAsync()
    {
        await _player!.StopVideoAsync();
    }
}
