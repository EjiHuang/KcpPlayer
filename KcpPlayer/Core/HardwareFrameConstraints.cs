using Sdcb.FFmpeg.Raw;
using System.Windows.Media;

namespace KcpPlayer.Core
{
    public class HardwareFrameConstraints
    {
        public AVPixelFormat[] ValidHardwareFormats { get; }
        public AVPixelFormat[] ValidSoftwareFormats { get; }

        public int MinWidth { get; }
        public int MinHeight { get; }

        public int MaxWidth { get; }
        public int MaxHeight { get; }

        public unsafe HardwareFrameConstraints(AVHWFramesConstraints* desc)
        {
            ValidHardwareFormats = Helpers.GetSpanFromSentinelTerminatedPtr(desc->valid_hw_formats, AVPixelFormat.None).ToArray();
            ValidSoftwareFormats = Helpers.GetSpanFromSentinelTerminatedPtr(desc->valid_sw_formats, AVPixelFormat.None).ToArray();
            MinWidth = desc->min_width;
            MinHeight = desc->min_height;
            MaxWidth = desc->max_width;
            MaxHeight = desc->max_height;
        }

        public bool IsValidDimensions(int width, int height)
        {
            return width >= MinWidth && width <= MaxWidth &&
                   height >= MinHeight && height <= MaxHeight;
        }
        public bool IsValidFormat(in AVPixelFormat format)
        {
            return Array.IndexOf(ValidSoftwareFormats, format) >= 0;
        }
    }
}
