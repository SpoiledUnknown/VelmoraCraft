using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelRenderEngine.src;
using VoxelRenderEngine.src.Graphics;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine
{
    internal class Game : GameWindow
    {
        private Chunk? chunk;

        private ShaderProgram? shaderProgram;

        //Camera
        private Camera? camera;

        private int width;
        private int height;
        public Game(int width, int height, string title, VSyncMode vsync, WindowState isFullscreen, WindowBorder windowBorder) : base(
                  GameWindowSettings.Default,
                  new NativeWindowSettings()
                  {
                      Title = title,
                      Vsync = vsync,
                      WindowState = isFullscreen,
                      WindowBorder = windowBorder,
                      StartFocused = true,
                      API = ContextAPI.OpenGL,
                      Profile = ContextProfile.Core
                  })
        {
            this.width = width;
            this.height = height;

            //Center The Window
            CenterWindow(new Vector2i(width, height));
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);

            width = e.Width;
            height = e.Height;

            base.OnResize(e);
        }

        protected override void OnLoad()
        {
            chunk = new Chunk(new Vector3(0, 0, 0));
            shaderProgram = new ShaderProgram("Default.vert", "Default.frag");

            GL.Enable(EnableCap.DepthTest);

            GL.FrontFace(FrontFaceDirection.Cw);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);

            camera =  new Camera(width, height, Vector3.Zero);
            CursorState = CursorState.Grabbed;

            base.OnLoad();
        }

        protected override void OnUnload()
        {
            shaderProgram!.Delete();
            chunk!.Delete();

            base.OnUnload();
        }

        protected override void OnRenderFrame(FrameEventArgs args)
        {
            GL.ClearColor(0.6f, 0.3f, 1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //transformation matices;
            Matrix4 model = Matrix4.Identity;
            Matrix4 view = camera!.getViewMatrix();
            Matrix4 projection = camera!.GetProjectionMatrix();

            int modelLocation = GL.GetUniformLocation(shaderProgram!.ID, "model");
            int viewLocation = GL.GetUniformLocation(shaderProgram!.ID, "view");
            int projectionLocation = GL.GetUniformLocation(shaderProgram!.ID, "projection");

            GL.UniformMatrix4(modelLocation, true, ref model);
            GL.UniformMatrix4(viewLocation, true, ref view);
            GL.UniformMatrix4(projectionLocation, true, ref projection);

            chunk!.Render(shaderProgram);

            Context.SwapBuffers();

            base.OnRenderFrame(args);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            MouseState mouse = MouseState;
            KeyboardState keyboard = KeyboardState;

            camera!.Update(keyboard, mouse, args);

            if (keyboard.IsKeyDown(Keys.Escape))
            {
                CursorState = CursorState.Normal;
            }
            else if (mouse.IsButtonDown(MouseButton.Left))
            {
                CursorState = CursorState.Grabbed;
            }

            base.OnUpdateFrame(args);
        }
    }
}
