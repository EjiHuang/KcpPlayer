using System.Diagnostics;

namespace KcpPlayer.KCP
{
    public class AvKcpServer
    {
        private KcpClient _client;

        private Task _taskForUpdateState;
        private Task? _taskForRecv;
        private CancellationTokenSource? _ctsForRecv;

        public AvKcpServer(int port, TraceListener? traceListener = null)
        {
            _client = new KcpClient(port);
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

        public void Init(int port, int channels, int samprate, int width, int height, int frate)
        {

        }

        public void Start()
        {
            _ctsForRecv = new CancellationTokenSource();
            _taskForRecv = Task.Run(Recv, _ctsForRecv.Token);

            Debug.WriteLine("[KCP] Server is running...");
        }

        public async Task ExitAsync()
        {
            if (_taskForRecv != null)
            {
                _ctsForRecv?.Cancel();
                _ctsForRecv = null;
                await _taskForRecv;
            }
        }

        private async void Recv()
        {
            while (_ctsForRecv != null && !_ctsForRecv.IsCancellationRequested)
            {
                var result = await _client.ReceiveAsync();
                var str = System.Text.Encoding.UTF8.GetString(result);
                Debug.WriteLine(str);
            }
        }
    }
}
