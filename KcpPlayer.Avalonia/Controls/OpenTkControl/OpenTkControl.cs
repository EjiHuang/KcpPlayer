using System;
using System.Diagnostics;
using Avalonia.Controls;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace KcpPlayer.Avalonia.Controls.OpenTkControl;

public class OpenTkControl : BaseTkOpenGlControl
{
    public Action? OnInitializing;
    public Action? OnRender;

    // Create the vertices for our triangle. These are listed in normalized device coordinates (NDC)
    // In NDC, (0, 0) is the center of the screen.
    // Negative X coordinates move to the left, positive X move to the right.
    // Negative Y coordinates move to the bottom, positive Y move to the top.
    // OpenGL only supports rendering in 3D, so to create a flat triangle, the Z coordinate will be kept as 0.
    private readonly float[] _vertices =
    {
        -0.5f, -0.5f, 0.0f, // Bottom-left vertex
         0.5f, -0.5f, 0.0f, // Bottom-right vertex
         0.0f,  0.5f, 0.0f  // Top vertex
    };

    // These are the handles to OpenGL objects. A handle is an integer representing where the object lives on the
    // graphics card. Consider them sort of like a pointer; we can't do anything with them directly, but we can
    // send them to OpenGL functions that need them.

    // What these objects are will be explained in OnLoad.
    private int _vertexBufferObject;
    private int _vertexArrayObject;

    protected override void OpenTkInit()
    {
        ChangeWindowTitle();

        OnInitializing?.Invoke();

        Debug.WriteLine("GL component initialized");
    }

    protected override void OpenTkRender()
    {
        OnRender?.Invoke();
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
