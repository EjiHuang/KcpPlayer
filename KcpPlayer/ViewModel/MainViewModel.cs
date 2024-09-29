using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Hexa.NET.Logging;
using KcpPlayer.Core;
using KcpPlayer.KCP;
using KcpPlayer.Utils;
using System.Collections.ObjectModel;
using System.Net;
using System.Windows;
using System.Windows.Input;

namespace KcpPlayer.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private static readonly ILogger _logger = LoggerFactory.GetLogger(nameof(MainViewModel));

        private FFmpegService _ffmpegService;
        private AvKcpServer? _avKcpServer;
        private AvKcpClient? _avKcpClient;

        public TextBlockTraceListener? TbTraceListener;

        public ObservableCollection<string> Urls { get; set; } = [];

        public MainViewModel()
        {
            _ffmpegService = new FFmpegService();

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

                    _avKcpClient = new AvKcpClient(port + 1, new IPEndPoint(IPAddress.Parse(ip), port), null);
                    _avKcpClient.Start();

                    //await _ffmpegService.DecodeFromStreamAsync(_avKcpClient.Stream);
                    await _ffmpegService.DecodeRTSPAsync("udp://127.0.0.1:40002");
                }
                else
                {
                    await _ffmpegService.DecodeRTSPAsync(Url);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                MessageBox.Show(ex.Message);
            }
        }

        private async Task VideoStopAsync()
        {
            await _ffmpegService.StopVideoAsync();
        }

        private void StartKcpServer()
        {
            _avKcpServer = new AvKcpServer(KcpPort, _ffmpegService);
            _avKcpServer.Start();

            KcpServerRunning = true;
        }

        public void VideoRender()
        {
            _ffmpegService.Render();
        }

        public int VideoWidth => _ffmpegService.VideoWidth;
        public int VideoHeight => _ffmpegService.VideoHeight;

        private string _url = "kcp://127.0.0.1:40001";//"rtsp://192.168.48.1:8554/channel=0";
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
        public ICommand VideoPlayCommand => _videoPlayCommand ??= new AsyncRelayCommand(VideoPlayAsync);

        private AsyncRelayCommand? _videoStopCommand;
        public ICommand VideoStopCommand => _videoStopCommand ??= new AsyncRelayCommand(VideoStopAsync);

        private RelayCommand? _startKcpServerCommand;
        public ICommand StartKcpServerCommand => _startKcpServerCommand ??= new RelayCommand(StartKcpServer);
    }
}
