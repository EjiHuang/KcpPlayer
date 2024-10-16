using System.IO;
using System.Threading.Tasks;

namespace KcpPlayer.Avalonia.Services;

public interface IMediaService
{
    public int VideoWidth { get; }
    public int VideoHeight { get; }
    public bool IsDecoding { get; }
    public Task DecodeFromStreamAsync(Stream stream);
    public Task DecodeUseRtspClientAsync(string url);
    public Task DecodeRtspAsync(string url);
    public Task StopVideoAsync();
    public void InitializeVideoStreamRenderer();
    public unsafe void RenderVideo();
    public void SetVideoSurfaceSize(int width, int height);
}
