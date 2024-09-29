using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace KcpPlayer.KCP
{
    public class AvKcpClient
    {
        private KcpClient _client;
        private Task _taskForUpdateState;
        private Task? _taskForRecv;
        private CancellationTokenSource? _ctsForRecv;

        public MemoryStream Stream { get; private set; }

        public AvKcpClient(int port, IPEndPoint endPoint, TraceListener? traceListener = null)
        {
            _client = new KcpClient(port, endPoint);
            if (traceListener == null)
            {
                _client.Kcp.TraceListener = new ConsoleTraceListener();
            }

            Stream = new MemoryStream();

            _taskForUpdateState = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                sw.Start();
                var interval = 1000;    // 1s

                var hb = Encoding.ASCII.GetBytes("hb");

                while (true)
                {
                    _client.Kcp.Update(DateTimeOffset.UtcNow);

                    if (sw.ElapsedMilliseconds >= interval)
                    {
                        _client.Send(hb, hb.Length);
                        sw.Restart();
                    }

                    await Task.Delay(10);
                }
            });
        }

        public void Start()
        {
            _ctsForRecv = new CancellationTokenSource();
            _taskForRecv = Task.Run(Recv, _ctsForRecv.Token);

            Debug.WriteLine("[KCP] Client is running...");
        }

        private async void Recv()
        {
            while (_ctsForRecv != null && !_ctsForRecv.IsCancellationRequested)
            {
                var data = await _client.ReceiveAsync();
                if (data != null)
                {
                    Stream.Write(data, 0, data.Length);
                }
            }
        }
    }
}
