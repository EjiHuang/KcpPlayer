using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KcpPlayer.Core;
using System.Windows;
using System.Windows.Input;

namespace KcpPlayer.ViewModel
{
    public class MainViewModel : ObservableObject
    {
        private FFmpegService _ffmpegService;

        public MainViewModel()
        {
            _ffmpegService = new FFmpegService();
        }

        private async Task VideoPlayAsync()
        {
            try
            {
                await _ffmpegService.DecodeRTSPAsync(Url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task VideoStopAsync()
        {
            await _ffmpegService.StopVideoAsync();
        }

        public void VideoRender()
        {
            _ffmpegService.Render();
        }

        public int VideoWidth => _ffmpegService.VideoWidth;
        public int VideoHeight => _ffmpegService.VideoHeight;

        private string _url = "rtsp://192.168.48.1:8554/channel=0";
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }

        private AsyncRelayCommand? _videoPlayCommand;
        public ICommand VideoPlayCommand => _videoPlayCommand ??= new AsyncRelayCommand(VideoPlayAsync);

        private AsyncRelayCommand? _videoStopCommand;
        public ICommand VideoStopCommand => _videoStopCommand ??= new AsyncRelayCommand(VideoStopAsync);
    }
}
