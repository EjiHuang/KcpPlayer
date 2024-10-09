using FFmpeg.AutoGen;
using FFmpeg.Wrapper;
using KcpPlayer.Avalonia.OpenGL;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Diagnostics;

namespace KcpPlayer.Avalonia.Services;

public class VideoStreamRendererService : IVideoStreamRendererService
{
    private bool _hasFrame = false;
    private bool _isHDR = false;

    private Texture2D _texY = null!;
    private Texture2D _texUV = null!;
    private ShaderProgram _shader;
    private BufferObject _emptyVbo;
    private VertexFormat _emptyVao;

    private bool _flushed = false;
    private SwScaler? _nv12SwScaler;

    public VideoStreamRendererService()
    {
        string shaderBasePath = AppContext.BaseDirectory + "Shaders/";
        _shader = new ShaderProgram();
        _shader.AttachFile(ShaderType.VertexShader, shaderBasePath + "full_screen_quad.vert");
        _shader.AttachFile(ShaderType.FragmentShader, shaderBasePath + "render_yuv.frag");
        _shader.Link();

        _emptyVbo = new BufferObject(16, BufferStorageFlags.None);
        _emptyVao = VertexFormat.CreateEmpty();
    }

    public void DrawTexture(VideoFrame decodedFrame)
    {
        if (decodedFrame.IsHardwareFrame)
        {
            //There's no easy way to interop between HW and GL surfaces, so we'll have to do a copy through the CPU here.
            //For what is worth, a 4K 60FPS P010 stream will in theory only take ~1400MB/s of bandwidth, which is not that bad.

            Debug.Assert(decodedFrame.IsHardwareFrame); //TODO: implement support for SW frames

            //TODO: Use TransferTo() when Map() fails.
            using var frame = decodedFrame.Map(
                HardwareFrameMappingFlags.Read | HardwareFrameMappingFlags.Direct
            )!;

            var (pixelType, pixelStride) = frame.PixelFormat switch
            {
                PixelFormats.NV12 => (PixelType.UnsignedByte, 1),
                PixelFormats.P010LE => (PixelType.UnsignedShort, 2),
            };

            bool highDepth = pixelStride == 2; //Don't downscale high bit-depth formats, otherwise we could end with ugly banding

            _texY ??= new Texture2D(
                frame.Width,
                frame.Height,
                1,
                highDepth ? SizedInternalFormat.R16 : SizedInternalFormat.R8
            );
            _texUV ??= new Texture2D(
                frame.Width / 2,
                frame.Height / 2,
                1,
                highDepth ? SizedInternalFormat.Rg16 : SizedInternalFormat.Rg8
            );

            _texY.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(0, out int strideY),
                0,
                0,
                frame.Width,
                frame.Height,
                PixelFormat.Red,
                pixelType,
                rowLength: strideY / pixelStride
            );

            _texUV.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(1, out int strideUV),
                0,
                0,
                frame.Width / 2,
                frame.Height / 2,
                PixelFormat.Rg,
                pixelType,
                rowLength: strideUV / pixelStride / 2
            );
        }
        else
        {
            if (_nv12SwScaler == null || !_nv12SwScaler.InputFormat.Equals(decodedFrame.Format))
            {
                var srcFmt = new PictureFormat(
                    decodedFrame.Width,
                    decodedFrame.Height,
                    decodedFrame.PixelFormat
                );
                var dstFmt = new PictureFormat(
                    decodedFrame.Width,
                    decodedFrame.Height,
                    AVPixelFormat.AV_PIX_FMT_NV12
                );
                _nv12SwScaler = new SwScaler(srcFmt, dstFmt, InterpolationMode.FastBilinear);
            }

            using var frame = new VideoFrame(
                decodedFrame.Width,
                decodedFrame.Height,
                AVPixelFormat.AV_PIX_FMT_NV12
            );
            _nv12SwScaler.Convert(decodedFrame, frame);

            var pixelStride = 1;
            var pixelType = PixelType.UnsignedByte;
            _texY ??= new Texture2D(frame.Width, frame.Height, 1, SizedInternalFormat.R8);
            _texUV ??= new Texture2D(frame.Width / 2, frame.Height / 2, 1, SizedInternalFormat.Rg8);

            _texY.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(0, out int strideY),
                0,
                0,
                frame.Width,
                frame.Height,
                PixelFormat.Red,
                pixelType,
                rowLength: strideY / pixelStride
            );

            _texUV.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(1, out int strideUV),
                0,
                0,
                frame.Width / 2,
                frame.Height / 2,
                PixelFormat.Rg,
                pixelType,
                rowLength: strideUV / pixelStride / 2
            );
        }

        _texY.BindUnit(0);
        _texUV.BindUnit(1);
        _shader.SetUniform("u_TextureY", 0);
        _shader.SetUniform("u_TextureUV", 1);
        _shader.SetUniform("u_ConvertHDRtoSDR", _isHDR ? 1 : 0);
        _shader.DrawArrays(PrimitiveType.Triangles, _emptyVao, _emptyVbo, 0, 3);
    }

    public void ReSetVideoSurfaceSize(int x, int y, int w, int h)
    {
        GL.Viewport(x, y, w, h);
    }
}
