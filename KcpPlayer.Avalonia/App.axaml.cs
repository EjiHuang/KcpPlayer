using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KcpPlayer.Avalonia.Services;
using KcpPlayer.Avalonia.ViewModels;
using KcpPlayer.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;

namespace KcpPlayer.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // �����ʹ��CommunityToolkit������Ҫ��������ɾ��Avalonia��������֤�����û����һ�У�������Avalonia��CT����ظ�����֤
        BindingPlugins.DataValidators.RemoveAt(0);

        // ע��Ӧ�ó���������������з���
        var services = ConfigureServices();

        var vm = services.GetRequiredService<MainWindowViewModel>();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                ViewModel = vm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ILogger>(_ =>
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    @$"logs\{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log",
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();
        });
        services.AddTransient<IMediaService, MediaService>();
        services.AddTransient<IVideoStreamRendererService, VideoStreamRendererService>();
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true }
        );
    }
}