using KcpPlayer.Utils;
using KcpPlayer.ViewModel;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Wpf;
using System.Windows;

namespace KcpPlayer.View
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : Window
    {
        private MainViewModel _viewModel;
        private double _dpiRatio = 1.0d;
        private TextBlockTraceListener _tbTraceListener;

        public MainView()
        {
            InitializeComponent();

            var glSettings = new GLWpfControlSettings
            {
                MajorVersion = 4,
                MinorVersion = 5,
                Profile = ContextProfile.Compatability,
                ContextFlags = ContextFlags.Debug
            };
            GlView.Start(glSettings);

            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _tbTraceListener = new TextBlockTraceListener(tb_TraceListener);
            _viewModel.TbTraceListener = _tbTraceListener;

            Loaded += MainView_Loaded;
        }

        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            _dpiRatio = source.CompositionTarget.TransformToDevice.M11;
        }

        private double _counter = 0d;
        private void GlView_Render(TimeSpan delta)
        {
            var videoWidth = _viewModel.VideoWidth;
            var videoHeight = _viewModel.VideoHeight;
            var clientWidth = (int)(GlView.ActualWidth * _dpiRatio);
            var clientHeight = (int)(GlView.ActualHeight * _dpiRatio);

            double scale = Math.Min(clientWidth / (double)videoWidth, clientHeight / (double)videoHeight);
            int w = (int)Math.Round(videoWidth * scale);
            int h = (int)Math.Round(videoHeight * scale);
            int x = (clientWidth - w) / 2;
            int y = (clientHeight - h) / 2;

            //FIXME: flickering after resize (caused by double buffering)
            GL.Viewport(0, 0, clientWidth, clientHeight);
            //GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Viewport(x, y, w, h);

            // Print some info
            _counter += delta.TotalMilliseconds;
            if (_counter > 1000)
            {
                var fps = 1000 / delta.TotalMilliseconds;
                tb_Resolution.Text = $"W {videoWidth} H {videoHeight}";
                tb_Fps.Text = $"FPS {fps:f1}";
                _counter = 0;
            }

            _viewModel.VideoRender();
        }
    }
}