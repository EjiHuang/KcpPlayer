using KcpPlayer.Services;
using KcpPlayer.ViewModels;
using KcpPlayer.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace KcpPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; private set; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ILogger>(_ =>
            {
                return new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(@$"logs\{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log", 
                        outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            });
            services.AddTransient<IMediaService, MediaService>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainView>();

            return services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true
            });
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var mainWindow = Services.GetService<MainView>();
            mainWindow!.Show();
        }
    }

}
