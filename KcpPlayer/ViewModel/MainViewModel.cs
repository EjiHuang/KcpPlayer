using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KcpPlayer.Core;
using OpenTK.Windowing.Desktop;
using System.Windows;
using System.Windows.Input;

namespace KcpPlayer.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private FFmpegService _ffmpegService;
        private CancellationTokenSource? _cts;

        public MainViewModel(IGLFWGraphicsContext gLFW)
        {
            _ffmpegService = new FFmpegService(gLFW);
        }

        private void VideoPlay()
        {
            try
            {
                _cts = new CancellationTokenSource();
                Task.Run(() => _ffmpegService.DecodeRTSP(Url, _cts.Token));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void VideoStop()
        {
            _cts?.Cancel();
        }

        public void VideoRender()
        {
            _ffmpegService.Render();
        }

        public int VideoWidth => _ffmpegService.VideoWidth;
        public int VideoHeight => _ffmpegService.VideoHeight;

        private string _url = "rtsp://127.0.0.1:8554/live/0";
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        private RelayCommand? _videoPlayCommand;
        public ICommand VideoPlayCommand => _videoPlayCommand ??= new RelayCommand(VideoPlay);

        private RelayCommand? _videoStopCommand;
        public ICommand VideoStopCommand => _videoStopCommand ??= new RelayCommand(VideoStop);
    }
}
