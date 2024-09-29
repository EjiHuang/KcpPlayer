using KcpPlayer.KCP;
using System.IO;

namespace KcpPlayer.Services
{
    public interface IMediaService
    {
        public int VideoWidth { get; }
        public int VideoHeight { get; }
        public bool IsDecoding { get; }
        public Task DecodeFromStreamAsync(Stream stream);
        public Task DecodeRTSPAsync(string url);
        public Task StopVideoAsync();
        public void InitializeVideoStreamRenderer();
        public unsafe void Render();
        public void RegisterAvKcpServer(AvKcpServer avKcpServer);
    }
}
