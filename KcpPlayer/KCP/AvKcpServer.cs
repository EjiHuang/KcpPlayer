using KcpPlayer.Core;
using System.Diagnostics;

namespace KcpPlayer.KCP
{
    public class AvKcpServer
    {
        private KcpClient _client;
        private FFmpegService _ffmpegService;

        private Task _taskForUpdateState;
        private Task? _taskForRecv;
        private CancellationTokenSource? _ctsForRecv;

        private bool _connected;

        public AvKcpServer(int port, FFmpegService ffmpegService, TraceListener? traceListener = null)
        {
            _client = new KcpClient(port);
            if (traceListener == null)
            {
                _client.Kcp.TraceListener = new ConsoleTraceListener();
            }

            _ffmpegService = ffmpegService;

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

        public void SendAvPacket(Span<byte> data)
        {
            if (!_connected)
                return;

            var ret = _client.Send(data, data.Length);
            if (ret < 0)
            {
                Debug.WriteLine("send failed...");
            }
        }

        private async void Recv()
        {
            while (_ctsForRecv != null && !_ctsForRecv.IsCancellationRequested)
            {
                var data = await _client.ReceiveAsync();
                if (data != null)
                {
                    var message = System.Text.Encoding.UTF8.GetString(data);
                    if (message == "hb")
                    {
                        _ffmpegService.RegisterAvKcpServer(this);
                        _connected = true;
                    }
                }
            }
        }
    }
}
