using System.Diagnostics;
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

        public AvKcpClient(int port, IPEndPoint endPoint, TraceListener? traceListener = null)
        {
            _client = new KcpClient(port, endPoint);
            if (traceListener != null)
            {
                _client.Kcp.TraceListener = traceListener;
            }

            _taskForUpdateState = Task.Run(async () =>
            {
                while (true)
                {
                    _client.Kcp.Update(DateTimeOffset.UtcNow);
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

        public void RequestVideoStream()
        {
            var hello = Encoding.UTF8.GetBytes("Hello server.");
            _client.Send(hello, hello.Length);
        }

        private async void Recv()
        {
            while (_ctsForRecv != null && !_ctsForRecv.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync();
                var str = Encoding.UTF8.GetString(result);
                Debug.WriteLine(str);
            }
        }
    }
}
