using Hexa.NET.Logging;
using System.Windows;

namespace KcpPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Config logger
            var logWriter = new LogFileWriter("logs");
            LoggerFactory.AddGlobalWriter(logWriter);
        }
    }

}
