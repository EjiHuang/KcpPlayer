using System.Threading.Tasks;

namespace KcpPlayer.Avalonia.Controls.OpenTkControl;

public interface IOpenTkPlayer
{
    public Task<bool> PlayVideoAsync(string videoPath);
}
