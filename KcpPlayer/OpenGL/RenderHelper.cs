using OpenTK.Graphics.OpenGL;

namespace KcpPlayer.OpenGL
{
    public class RenderHelper
    {
        private int _elementBufferObject;

        private int _vertexBufferObject;

        private int _vertexArrayObject;

        private readonly float[] _vertices =
        {
            // Position     Texture coordinates
            -1f, -1f, 1.0f, 0.0f, 1.0f, // top right
            -1f,  1f, 1.0f, 0.0f, 0.0f, // bottom right
             1f,  1f, 1.0f, 1.0f, 0.0f, // bottom left
             1f, -1f, 1.0f, 1.0f, 1.0f  // top left
        };//顶点信息

        private readonly uint[] _indices =
        {
            0, 1, 2,
            2, 3, 0
        };

        public Shader shader;
        public Texture texture;

        public RenderHelper()
        {
            InitBuffer();

            shader = new Shader("Shaders/full_screen_quad.vert", "Shaders/render_yuv.frag");
            shader.Use();
            BindValue();

            texture = Texture.CreateTexture();
        }

        private void InitBuffer()
        {
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);

            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);

            _elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

        }

        private void BindValue()
        {
            var vertexLocation = shader.GetAttribLocation("aPosition");
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
            GL.EnableVertexAttribArray(vertexLocation);

            var texCoordLocation = shader.GetAttribLocation("aTexCoord");
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(texCoordLocation);
        }

        public void DrawTexture(int width, int height, byte[] image)
        {
            GL.BindVertexArray(_vertexArrayObject);

            //_texture.Change(OpenTK.Graphics.OpenGL4.TextureUnit.Texture0,width,height,image);

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle);

            GL.TexImage2D<byte>(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image);

            texture.Use(TextureUnit.Texture0);
            shader.Use();

            GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
        }
    }
}
