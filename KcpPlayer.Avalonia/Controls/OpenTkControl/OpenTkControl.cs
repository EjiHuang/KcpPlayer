using Avalonia.Controls;
using Avalonia.OpenGL;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Diagnostics;

namespace KcpPlayer.Avalonia.Controls.OpenTkControl;

public class OpenTkControl : BaseTkOpenGlControl
{
    public Action? OnInitializing;
    public Action? OnRender;

    protected override void OpenTkInit()
    {
        ChangeWindowTitle();

        OnInitializing?.Invoke();

        Debug.WriteLine($"GL component initialized. OpenGL version: {GlVersion.Major}.{GlVersion.Minor}");
    }

    protected override void OpenTkRender()
    {
        //GL.Enable(EnableCap.DepthTest);
        //GL.Enable(EnableCap.CullFace);

        //GL.ClearColor(new OpenTK.Mathematics.Color4(0, 32, 48, 255));
        //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        OnRender?.Invoke();

        //GL.Disable(EnableCap.DepthTest);
    }

    protected override void OpenTkTeardown()
    {
        Debug.WriteLine("Tearning down gl component");
    }

    private void ChangeWindowTitle()
    {
        if (this.VisualRoot is Window window)
            window.Title += " OpenGL Version: " + GL.GetString(StringName.Version);
    }
}
