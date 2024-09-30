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
        // 如果您使用CommunityToolkit，则需要以下行来删除Avalonia的数据验证，如果没有这一行，您将从Avalonia和CT获得重复的验证
        BindingPlugins.DataValidators.RemoveAt(0);

        // 注册应用程序运行所需的所有服务
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