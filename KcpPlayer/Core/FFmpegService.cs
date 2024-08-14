using FFmpeg.Wrapper;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KcpPlayer.Core
{
    public class FFmpegService
    {
        private ConcurrentQueue<VideoFrame> _videoFrames = new ConcurrentQueue<VideoFrame>();

        private MediaDemuxer? _demuxer;
        private HardwareDevice? _hwDevice;
        private MediaStream? _stream;
        private VideoDecoder? _decoder;
        private SwScaler? _swScaler = null;
        private Task? _decodeLoop;
        private CancellationTokenSource? _ctsForDecodeLoop;

        private VideoStreamRenderer _renderHelper;

        public int VideoWidth { get; private set; }
        public int VideoHeight { get; private set; }
        public bool IsDecoding { get; private set; }

        public FFmpegService()
        {
            FFmpeg.AutoGen.ffmpeg.RootPath = AppContext.BaseDirectory + "runtimes/win-x64/native";

            _renderHelper = new VideoStreamRenderer();
        }

        public async Task DecodeRTSPAsync(string url)
        {
            _demuxer = await Task.Run(() => new MediaDemuxer(url));

            _stream = _demuxer.FindBestStream(MediaTypes.Video);
            if (_stream == null) return;

            _decoder = (VideoDecoder)_demuxer.CreateStreamDecoder(_stream, open: false);
            if (_decoder == null) return;

            var hwConfig = _decoder.GetHardwareConfigs().FirstOrDefault(config => config.DeviceType == HWDeviceTypes.DXVA2);
            _hwDevice = HardwareDevice.Create(hwConfig.DeviceType);

            if (_hwDevice != null)
            {
                _decoder.SetupHardwareAccelerator(hwConfig, _hwDevice);
            }
            _decoder.Open();
            
            VideoWidth = _decoder.Width;
            VideoHeight = _decoder.Height;

            _ctsForDecodeLoop = new CancellationTokenSource();
            _decodeLoop = Task.Run(DecodeLoop, _ctsForDecodeLoop.Token);
        }

        public async Task StopVideoAsync()
        {
            if (IsDecoding)
            {
                _ctsForDecodeLoop?.Cancel();
                await _decodeLoop!;
                _ctsForDecodeLoop = null;

                Free();
            }
        }

        private void Free()
        {
            _decoder?.Dispose();
            _demuxer?.Dispose();

            _swScaler?.Dispose();
            _swScaler = null;
        }

        private void DecodeLoop()
        {
            IsDecoding = true;

            while (_ctsForDecodeLoop != null && !_ctsForDecodeLoop.IsCancellationRequested)
            {
                var frame = new VideoFrame();
                using var packet = new MediaPacket();

                try
                {
                    if (_demuxer!.Read(packet))
                    {
                        if (packet.StreamIndex != _stream!.Index)
                            continue;

                        _decoder!.SendPacket(packet);
                        if (_decoder!.ReceiveFrame(frame))
                        {
                            _videoFrames.Enqueue(frame);
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            IsDecoding = false;
        }

        public unsafe void Render()
        {
            if (_videoFrames.TryDequeue(out var frame))
            {
                _renderHelper.DrawTexture(frame);
                frame.Dispose();
            }
        }
    }
}
