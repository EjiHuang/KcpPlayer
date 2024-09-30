using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Net.Sockets.Kcp;

namespace KcpPlayer.KCP
{
    public class KcpClient : IKcpCallback
    {
        private UdpClient _client;

        public SimpleSegManager.Kcp Kcp { get; private set; }
        public IPEndPoint? EndPoint { get; set; }

        public KcpClient(int port)
            : this(port, null)
        {

        }

        public KcpClient(int port, IPEndPoint? endPoint)
        {
            _client = new UdpClient(port);
            Kcp = new SimpleSegManager.Kcp(2001, this);
            EndPoint = endPoint;
            BeginRecv();
        }

        private async void BeginRecv()
        {
            var res = await _client.ReceiveAsync();
            EndPoint = res.RemoteEndPoint;
            Kcp.Input(res.Buffer);
            BeginRecv();
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            var s = buffer.Memory.Span.Slice(0, avalidLength).ToArray();
            _client.SendAsync(s, s.Length, EndPoint);
            buffer.Dispose();
        }

        public int Send(byte[] datagram, int bytes)
        {
            return Kcp.Send(datagram.AsSpan().Slice(0, bytes));
        }

        public int Send(Span<byte> datagram, int bytes)
        {
            return Kcp.Send(datagram.Slice(0, bytes));
        }

        public async ValueTask<byte[]> ReceiveAsync()
        {
            var (buffer, avalidLength) = Kcp.TryRecv();
            while (buffer == null)
            {
                await Task.Delay(10);
                (buffer, avalidLength) = Kcp.TryRecv();
            }

            var s = buffer.Memory.Span.Slice(0, avalidLength).ToArray();
            return s;
        }
    }
}
