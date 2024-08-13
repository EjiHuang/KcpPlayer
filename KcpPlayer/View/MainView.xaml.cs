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

            _viewModel = new MainViewModel((OpenTK.Windowing.Desktop.IGLFWGraphicsContext)GlView.Context!);
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this, Wpf.Ui.Controls.WindowBackdropType.Mica, true);
        }

        private void GlView_Render(TimeSpan delta)
        {
            var videoWidth = _viewModel.VideoWidth;
            var videoHeight = _viewModel.VideoHeight;
            var clientWidth = (int)GlView.ActualWidth;
            var clientHeight = (int)GlView.ActualHeight;

            double scale = Math.Min(clientWidth / (double)videoWidth, clientHeight / (double)videoHeight);
            int w = (int)Math.Round(videoWidth * scale);
            int h = (int)Math.Round(videoHeight * scale);
            int x = (clientWidth - w) / 2;
            int y = (clientHeight - h) / 2;

            //FIXME: flickering after resize (caused by double buffering)
            GL.Viewport(0, 0, clientWidth, clientHeight);
            //GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.Viewport(x, y, w, h);

            _viewModel.VideoRender();
        }
    }
}