using KcpPlayer.OpenGL;
using OpenTK.Windowing.Desktop;
using Sdcb.FFmpeg.Codecs;
using Sdcb.FFmpeg.Formats;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Toolboxs.Extensions;
using Sdcb.FFmpeg.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;

namespace KcpPlayer.Core
{
    public class FFmpegService
    {
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private ConcurrentQueue<Frame> _videoFrames = new ConcurrentQueue<Frame>();

        private VideoStreamRenderer _renderHelper;

        public int VideoWidth { get; private set; }
        public int VideoHeight { get; private set; }

        public FFmpegService(IGLFWGraphicsContext gLFW)
        {
            FFmpegLogger.LogWriter = (level, msg) => Debug.WriteLine($"{level} {msg}");

            _renderHelper = new VideoStreamRenderer(gLFW);
        }

        public unsafe void DecodeRTSP(string url, CancellationToken cancellationToken = default)
        {
            MediaDictionary options = new MediaDictionary();
            if (url.StartsWith("rtsp", StringComparison.CurrentCultureIgnoreCase))
            {
                options.Set("fflags", "nobuffer");
                options.Set("timeout", "5000000");
                options.Set("rtsp_flags", "prefer_tcp");
            }
            else if (url.StartsWith("rtmp", StringComparison.CurrentCultureIgnoreCase))
            {
                options.Set("fflags", "nobuffer");
                options.Set("rw_timeout", "1000000");
            }

            using FormatContext fc = FormatContext.OpenInputUrl(url, options: options);
            fc.LoadStreamInfo();
            MediaStream videoStream = fc.GetVideoStream();

            using CodecContext videoDecoder = new CodecContext(Codec.FindDecoderById(videoStream.Codecpar!.CodecId));
            videoDecoder.FillParameters(videoStream.Codecpar!);

            using var device = HardwareDevice.Create(AVHWDeviceType.Dxva2);
            if (device != null)
            {
                SetupHardwareAccelerator(videoDecoder, device);
            }

            videoDecoder.Open();
            
            VideoWidth = videoDecoder.Width;
            VideoHeight = videoDecoder.Height;

            foreach (var frame in fc
                .ReadPackets(videoStream.Index)
                .DecodePackets(videoDecoder))
            {
                if (cancellationToken.IsCancellationRequested) break;

                //Debug.WriteLine($"fmt:{(AVPixelFormat)frame.Format} w:{frame.Width} h:{frame.Height}");

                _videoFrames.Enqueue(frame.Clone());

                frame.Unref();
            }
        }

        public unsafe void Render()
        {
            if (_videoFrames.TryDequeue(out var frame))
            {
                _renderHelper.DrawTexture(new VideoFrame((AVFrame*)frame, false));
                frame.Unref();
            }
        }

        //Used to prevent callback pointer from being GC collected
        AVCodecContext_get_format? _chooseHwPixelFmt;
        private unsafe void SetupHardwareAccelerator(CodecContext codec, HardwareDevice device)
        {
            codec.HwDeviceContext = BufferRef.FromNativeOrNull(device.Handle, false);
            ((AVCodecContext*)codec)->get_format = _chooseHwPixelFmt = (ctx, pAvailFmts) =>
            {
                for (var pFmt = pAvailFmts; *pFmt != AVPixelFormat.None; pFmt++)
                {
                    if (*pFmt == AVPixelFormat.Dxva2Vld)
                    {
                        return *pFmt;
                    }
                }
                return ctx->sw_pix_fmt;
            };
        }
    }
}
