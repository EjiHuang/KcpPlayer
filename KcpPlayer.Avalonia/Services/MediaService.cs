using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.Wrapper;
using KcpPlayer.Avalonia.Utils;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KcpPlayer.Avalonia.Services;

public class MediaService : IMediaService
{
    private readonly ILogger _logger;

    private readonly ConcurrentQueue<VideoFrame> _videoFrames = new();

    private MediaDemuxer? _demuxer;
    private HardwareDevice? _hwDevice;
    private MediaStream? _stream;
    private VideoDecoder? _decoder;
    private SwScaler? _swScaler = null;
    private Task? _decodeLoop;
    private CancellationTokenSource? _ctsForDecodeLoop;
    private IOContext? _ioContext;

    private VideoStreamRendererService? _renderHelper;

    private readonly RTSPClient _rtspClient;
    private readonly ConcurrentQueue<byte[]> _naluQueue = new();
    private AsyncAutoResetEvent? _asyncAutoResetEvent;

    public int VideoWidth { get; private set; }
    public int VideoHeight { get; private set; }
    public bool IsDecoding { get; private set; }

    public MediaService()
    {
        _logger = App.ServiceProvider!.GetRequiredService<ILogger>();
        FFmpeg.AutoGen.ffmpeg.RootPath = AppContext.BaseDirectory + "runtimes/win-x64/native";

        _rtspClient = new RTSPClient(_logger);
        InitializeRtspClient();
    }

    public void InitializeVideoStreamRenderer()
    {
        _renderHelper = new VideoStreamRendererService();
    }

    private async Task DecodeFromQueueAsync(ConcurrentQueue<byte[]> queue)
    {
        _ioContext = IOContext.CreateInputFromQueue(queue);
        _demuxer = await Task.Run(() => new MediaDemuxer(_ioContext));
        StartDecode();
    }

    public async Task DecodeFromStreamAsync(Stream stream)
    {
        _ioContext = IOContext.CreateInputFromStream(stream);
        _demuxer = await Task.Run(() => new MediaDemuxer(_ioContext));
        StartDecode();
    }

    public async Task DecodeRtspAsync(string url)
    {
        _demuxer = await Task.Run(() => new MediaDemuxer(url));
        StartDecode();
    }

    public async Task DecodeUseRtspClientAsync(string url)
    {
        _asyncAutoResetEvent = new AsyncAutoResetEvent(false);
        _rtspClient.Connect(
            url,
            "admin",
            "admin",
            RTSPClient.RTP_TRANSPORT.TCP,
            RTSPClient.MEDIA_REQUEST.VIDEO_ONLY
        );

        if (await _asyncAutoResetEvent.WaitAsync(TimeSpan.FromMilliseconds(5000)))
        {
            _asyncAutoResetEvent = null;
            await DecodeFromQueueAsync(_naluQueue);
        }
        else
        {
            _rtspClient.Stop();
        }
    }

    private void StartDecode()
    {
        if (_demuxer != null)
        {
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
    }

    public async Task StopVideoAsync()
    {
        if (!_rtspClient.StreamingFinished())
        {
            _rtspClient.Stop();
        }

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

                        lock (_lock)
                        {
                            while (
                                _videoFrames.Count > 1
                                && _videoFrames.TryDequeue(out var savedFrame)
                            )
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

    #region RTSP

    private void InitializeRtspClient()
    {
        _rtspClient.NewVideoStream += OnNewVideoStream;
        _rtspClient.SetupMessageCompleted += OnSetupMessageCompleted;
        _rtspClient.ReceivedVideoData += OnReceivedVideoData;
    }

    private void OnReceivedVideoData(object? sender, SimpleDataEventArgs e)
    {
        foreach (var nalUnitMem in e.Data)
        {
            var nalUnit = nalUnitMem.Span;
            // Output some H264 stream information
            if (nalUnit.Length > 5)
            {
                int nal_ref_idc = (nalUnit[4] >> 5) & 0x03;
                int nal_unit_type = nalUnit[4] & 0x1F;
                string description = nal_unit_type switch
                {
                    1 => "NON IDR NAL",
                    5 => "IDR NAL",
                    6 => "SEI NAL",
                    7 => "SPS NAL",
                    8 => "PPS NAL",
                    9 => "ACCESS UNIT DELIMITER NAL",
                    _ => "OTHER NAL",
                };
                _logger.Information(
                    "NAL Ref = {nal_ref_idc} NAL Type = {nal_unit_type} {description}",
                    nal_ref_idc,
                    nal_unit_type,
                    description
                );

                _naluQueue.Enqueue(nalUnit.ToArray());

                if (nal_unit_type == 5)
                {
                    _asyncAutoResetEvent?.Set();
                }
            }
        }
    }

    private void OnSetupMessageCompleted(object? sender, EventArgs e)
    {
        _rtspClient.Play();
    }

    private void OnNewVideoStream(object? sender, NewStreamEventArgs e)
    {
        switch (e.StreamType)
        {
            case "H264":
                NewH264Stream(e, _rtspClient);
                break;
            default:
                _logger.Warning("Unknow Video format {streamtype}", e.StreamType);
                break;
        }
    }

    private void NewH264Stream(NewStreamEventArgs e, RTSPClient client)
    {
        _naluQueue.Clear();
        if (e.StreamConfigurationData is H264StreamConfigurationData h264StreamConfigurationData)
        {
            var sps = h264StreamConfigurationData.SPS;
            var pps = h264StreamConfigurationData.PPS;
            var startCode = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            var totalLength = 4 + sps.Length + 4 + pps.Length;
            var spsPps = new byte[totalLength];

            Buffer.BlockCopy(startCode, 0, spsPps, 0, 4);
            Buffer.BlockCopy(sps, 0, spsPps, 4, sps.Length);
            Buffer.BlockCopy(startCode, 0, spsPps, 4 + sps.Length, 4);
            Buffer.BlockCopy(pps, 0, spsPps, 4 + sps.Length + 4, pps.Length);

            _naluQueue.Enqueue(spsPps);
        }
    }

    #endregion

    #region Render

    private static object _lock = new object();

    public unsafe void RenderVideo()
    {
        lock (_lock)
        {
            if (_videoFrames.TryPeek(out var frame) && _renderHelper != null)
            {
                _renderHelper.DrawTexture(frame);
            }
        }
    }

    public void SetVideoSurfaceSize(int width, int height)
    {
        var scale = Math.Min(width / (double)VideoWidth, height / (double)VideoHeight);

        int w = (int)Math.Round(VideoWidth * scale);
        int h = (int)Math.Round(VideoHeight * scale);
        int x = (width - w) / 2;
        int y = (height - h) / 2;

        _renderHelper?.ReSetVideoSurfaceSize(x, y, w, h);
        //Debug.WriteLine($"{x} {y} {w} {h}");
    }

    #endregion
}
