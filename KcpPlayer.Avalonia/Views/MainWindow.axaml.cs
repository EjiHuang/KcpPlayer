using Avalonia.Controls;
using KcpPlayer.Avalonia.ViewModels;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

namespace KcpPlayer.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        private double _dpiRatio = 1.0d;
        private int _openTkControlWidth;
        private int _openTkControlHeight;

        private MainWindowViewModel? _viewModel;
        public MainWindowViewModel? ViewModel
        {
            get => _viewModel;
            set
            {
                _viewModel = value;
                DataContext = _viewModel;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            OpenGlControlHost.OnInitializing += OnOpenGlControlInitializing;
            OpenGlControlHost.OnRender += OnOpenGlRender;
        }

        private void OnOpenGlRender()
        {
            var scaling = GetTopLevel(OpenGlControlHost)!.RenderScaling;
            var videoWidth = ViewModel!.VideoWidth;
            var videoHeight = ViewModel.VideoHeight;
            var clientWidth = Math.Max(1, (int)(OpenGlControlHost.Bounds.Width * scaling));
            var clientHeight = Math.Max(1, (int)(OpenGlControlHost.Bounds.Height * scaling));
            var scale = Math.Min(
                clientWidth / (double)videoWidth,
                clientHeight / (double)videoHeight
            );

            int w = (int)Math.Round(videoWidth * scale);
            int h = (int)Math.Round(videoHeight * scale);
            int x = (clientWidth - w) / 2;
            int y = (clientHeight - h) / 2;

            if (_openTkControlWidth != w || _openTkControlHeight != h)
            {
                _openTkControlWidth = w;
                _openTkControlHeight = h;

                //FIXME: flickering after resize (caused by double buffering)
                GL.Viewport(0, 0, clientWidth, clientHeight);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                GL.Viewport(x, y, w, h);
            }

            // 渲染视频帧
            ViewModel.VideoRender();
        }

        private void OnOpenGlControlInitializing()
        {
            // 获取缩放比率
            _dpiRatio = RenderScaling;

            // 初始化渲染器
            ViewModel!.InitialiazeRenderer();
        }
    }
}
