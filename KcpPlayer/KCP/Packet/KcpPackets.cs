using System.Runtime.InteropServices;

namespace KcpPlayer.KCP.Packet
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct KcpVideoRequest
	{
		public byte PlayFlag;
	}
}
