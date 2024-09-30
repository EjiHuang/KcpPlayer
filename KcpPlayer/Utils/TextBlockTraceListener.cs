using System.Diagnostics;
using System.Windows.Controls;

namespace KcpPlayer.Utils
{
    public class TextBlockTraceListener : TraceListener
    {
        private TextBlock _output;

        public TextBlockTraceListener(TextBlock output)
        {
            Name = "Trace";
            _output = output;
        }

        public override void Write(string? message)
        {
            Action append = delegate () {
                _output.Text += string.Format("[{0}] ", DateTime.Now.ToString("HH:mm:ss:fff")) + message;
            };
            _output.Dispatcher.BeginInvoke(append);
        }

        public override void WriteLine(string? message)
        {
            Write(message + Environment.NewLine);
        }
    }
}
