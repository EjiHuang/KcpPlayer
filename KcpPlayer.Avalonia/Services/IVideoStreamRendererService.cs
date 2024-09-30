using FFmpeg.Wrapper;

namespace KcpPlayer.Avalonia.Services;

public interface IVideoStreamRendererService
{
    public void DrawTexture(VideoFrame decodedFrame);
}
