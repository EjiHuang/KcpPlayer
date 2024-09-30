using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KcpPlayer.Core;
using KcpPlayer.KCP;
using KcpPlayer.Services;
using KcpPlayer.Utils;
using Serilog;

namespace KcpPlayer.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly ILogger _logger;
        private readonly IMediaService _mediaService;
        private AvKcpServer? _avKcpServer;
        private AvKcpClient? _avKcpClient;

        public TextBlockTraceListener? TbTraceListener;

        public ObservableCollection<string> Urls { get; set; } = [];

        public MainViewModel(ILogger logger, IMediaService mediaService)
        {
            _logger = logger;
            _mediaService = mediaService;

            // 初始化数据绑定
            Urls.Add("rtsp://rtspstream:f653638d5e1d579e7ba0aaf97e9e54ac@zephyr.rtsp.stream/movie");
            Urls.Add(
                "rtsp://rtspstream:ab0fed99d825e52d589af4e91a1842d0@zephyr.rtsp.stream/pattern"
            );
            Urls.Add("kcp://127.0.0.1:40001");
            var cameraDevices = FFmpegCameraManager.GetCameraDevices();
            if (cameraDevices != null)
            {
                foreach (var camera in cameraDevices)
                {
                    Urls.Add(camera.Path);
                }
            }
        }

        private async Task VideoPlayAsync()
        {
            try
            {
                if (Url.StartsWith("kcp://", StringComparison.InvariantCultureIgnoreCase))
                {
                    var uri = new Uri(Url.ToLower());
                    var ip = uri.Host;
                    var port = uri.Port;

                    _avKcpClient = new AvKcpClient(
                        port + 1,
                        new IPEndPoint(IPAddress.Parse(ip), port),
                        null
                    );
                    _avKcpClient.Start();

                    //await _ffmpegService.DecodeFromStreamAsync(_avKcpClient.Stream);
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
                MessageBox.Show(ex.Message);
            }
        }

        private async Task VideoStopAsync()
        {
            await _mediaService.StopVideoAsync();
        }

        private void StartKcpServer()
        {
            _avKcpServer = new AvKcpServer(KcpPort, _mediaService);
            _avKcpServer.Start();

            KcpServerRunning = true;
        }

        public void InitialiazeRenderer()
        {
            // 初始化渲染器
            _mediaService.InitializeVideoStreamRenderer();
        }

        public void VideoRender()
        {
            _mediaService.Render();
        }

        public int VideoWidth => _mediaService.VideoWidth;
        public int VideoHeight => _mediaService.VideoHeight;

        private string _url = "kcp://127.0.0.1:40001"; //"rtsp://192.168.48.1:8554/channel=0";
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        private int _kcpPort = 40001;
        public int KcpPort
        {
            get => _kcpPort;
            set => SetProperty(ref _kcpPort, value);
        }

        private bool _kcpServerRunning;
        public bool KcpServerRunning
        {
            get => _kcpServerRunning;
            set => SetProperty(ref _kcpServerRunning, value);
        }

        private AsyncRelayCommand? _videoPlayCommand;
        public ICommand VideoPlayCommand =>
            _videoPlayCommand ??= new AsyncRelayCommand(VideoPlayAsync);

        private AsyncRelayCommand? _videoStopCommand;
        public ICommand VideoStopCommand =>
            _videoStopCommand ??= new AsyncRelayCommand(VideoStopAsync);

        private RelayCommand? _startKcpServerCommand;
        public ICommand StartKcpServerCommand =>
            _startKcpServerCommand ??= new RelayCommand(StartKcpServer);
    }
}
