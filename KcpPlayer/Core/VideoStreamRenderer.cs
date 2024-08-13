using KcpPlayer.OpenGL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using Sdcb.FFmpeg.Raw;
using Sdcb.FFmpeg.Utils;

namespace KcpPlayer.Core
{
    public class VideoStreamRenderer
    {
        private bool _hasFrame = false;
        private bool _isHDR = false;

        private Texture2D _texY = null!, _texUV = null!;
        private ShaderProgram _shader;
        private BufferObject _emptyVbo;
        private VertexFormat _emptyVao;

        private IGLFWGraphicsContext _glfw;
        private bool _flushed = false;

        public VideoStreamRenderer(IGLFWGraphicsContext gLFW)
        {
            string shaderBasePath = AppContext.BaseDirectory + "Shaders/";
            _shader = new ShaderProgram();
            _shader.AttachFile(ShaderType.VertexShader, shaderBasePath + "full_screen_quad.vert");
            _shader.AttachFile(ShaderType.FragmentShader, shaderBasePath + "render_yuv.frag");
            _shader.Link();

            _emptyVbo = new BufferObject(16, BufferStorageFlags.None);
            _emptyVao = VertexFormat.CreateEmpty();

            _glfw = gLFW;
        }

        public void DrawTexture(VideoFrame decodedFrame)
        {
            var width = decodedFrame.Width;
            var height = decodedFrame.Height;

            //TODO: Use TransferTo() when Map() fails.
            using var frame = decodedFrame.Map(HardwareFrameMappingFlags.Read | HardwareFrameMappingFlags.Direct)!;

            var (pixelType, pixelStride) = frame.PixelFormat switch
            {
                AVPixelFormat.Nv12 => (PixelType.UnsignedByte, 1),
                AVPixelFormat.P010le => (PixelType.UnsignedShort, 2)
            };

            bool highDepth = pixelStride == 2; //Don't downscale high bit-depth formats, otherwise we could end with ugly banding

            _texY ??= new Texture2D(width, height, 1, SizedInternalFormat.R8);
            _texUV ??= new Texture2D(width / 2, height / 2, 1, SizedInternalFormat.R8);

            _texY.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(0, out int strideY),
                0, 0, width, height,
                PixelFormat.Red, pixelType, rowLength: strideY / pixelStride);

            _texUV.SetPixels<byte>(
                frame.GetPlaneSpan<byte>(1, out int strideUV),
                0, 0, width / 2, height / 2,
                PixelFormat.Rg, pixelType, rowLength: strideUV / pixelStride / 2);

            _texY.BindUnit(0);
            _texUV.BindUnit(1);
            _shader.SetUniform("u_TextureY", 0);
            _shader.SetUniform("u_TextureUV", 1);
            _shader.SetUniform("u_ConvertHDRtoSDR", _isHDR ? 1 : 0);
            _shader.DrawArrays(PrimitiveType.Triangles, _emptyVao, _emptyVbo, 0, 3);

            _glfw.SwapBuffers();
        }
    }
}
