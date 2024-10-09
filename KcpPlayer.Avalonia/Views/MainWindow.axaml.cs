using Avalonia.Controls;
using KcpPlayer.Avalonia.ViewModels;

namespace KcpPlayer.Avalonia.Views
{
    public partial class MainWindow : Window
    {
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

            Loaded += (_, _) =>
            {
                _viewModel?.SetOpenTkPlayer(OpenGlControlHost);
            };
        }
    }
}
