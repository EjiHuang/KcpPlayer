using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.Wrapper;

namespace KcpPlayer.Avalonia.Services;

public class MediaService : IMediaService
{
    private ConcurrentQueue<VideoFrame> _videoFrames = new ConcurrentQueue<VideoFrame>();

    private MediaDemuxer? _demuxer;
    private HardwareDevice? _hwDevice;
    private MediaStream? _stream;
    private VideoDecoder? _decoder;
    private SwScaler? _swScaler = null;
    private Task? _decodeLoop;
    private CancellationTokenSource? _ctsForDecodeLoop;

    private MemoryStream? _streamStream;
    private IOContext? _ioContext;

    private VideoStreamRendererService? _renderHelper;

    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }
    public bool IsDecoding { get; private set; }

    public MediaService()
    {
        FFmpeg.AutoGen.ffmpeg.RootPath = AppContext.BaseDirectory + "runtimes/win-x64/native";
    }

    public void InitializeVideoStreamRenderer()
    {
        _renderHelper = new VideoStreamRendererService();
    }

    public async Task DecodeFromStreamAsync(Stream stream)
    {
        _ioContext = IOContext.CreateInputFromStream(stream);
        _demuxer = await Task.Run(() => new MediaDemuxer(_ioContext));
    }

    public async Task DecodeRTSPAsync(string url)
    {
        _demuxer = await Task.Run(() => new MediaDemuxer(url));

        _stream = _demuxer.FindBestStream(MediaTypes.Video);
        if (_stream == null)
            return;

        _decoder = (VideoDecoder)_demuxer.CreateStreamDecoder(_stream, open: false);
        if (_decoder == null)
            return;

        var hwConfigs = _decoder.GetHardwareConfigs();
        if (hwConfigs != null && hwConfigs.Count > 0)
        {
            var hwConfig = hwConfigs.FirstOrDefault(config =>
                config.DeviceType == HWDeviceTypes.DXVA2
            );
            _hwDevice = HardwareDevice.Create(hwConfig.DeviceType);

            if (_hwDevice != null)
            {
                _decoder.SetupHardwareAccelerator(hwConfig, _hwDevice);
            }
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
                        //_callbackForRender?.Invoke();

                        lock (_lock)
                        {
                            while (_videoFrames.Count > 1 && _videoFrames.TryDequeue(out var savedFrame))
                            {
                                savedFrame.Dispose();
                            }
                        }
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

    private static object _lock = new object();
    private Action? _callbackForRender;

    public unsafe void RenderVideo(int width, int height)
    {
        lock (_lock)
        {
            if (_videoFrames.TryPeek(out var frame) && _renderHelper != null)
            {
                _renderHelper.DrawTexture(frame);
            }
        }

        //if (_videoFrames.TryPeek(out var frame) && _renderHelper != null)
        //{
        //    if (_videoFrames.Count == 0)
        //    {
        //        _renderHelper.DrawTexture(frame);
        //    }

        //    frame.Dispose();
        //}

        //if (_videoFrames.TryDequeue(out var frame) && _renderHelper != null)
        //{
        //    _renderHelper.DrawTexture(frame);
        //    frame.Dispose();
        //}
    }

    internal void SetRenderCallback(Action value)
    {
        _callbackForRender = value;
    }

    internal void SetVideoSurfaceSize(int width, int height)
    {
        var scale = Math.Min(
            width / (double)VideoWidth,
            height / (double)VideoHeight
        );

        int w = (int)Math.Round(VideoWidth * scale);
        int h = (int)Math.Round(VideoHeight * scale);
        int x = (width - w) / 2;
        int y = (height - h) / 2;

        _renderHelper?.ReSetVideoSurfaceSize(x, y, w, h);
        //Debug.WriteLine($"{x} {y} {w} {h}");
    }
}
