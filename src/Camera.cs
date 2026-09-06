using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace VoxelRenderEngine.src
{
    internal class Camera
    {
        private float speed = 8f;
        private float screenWidth;
        private float screenHeight;
        private float sensitivity = 10f;

        private Vector3 position;

        private Vector3 up = Vector3.UnitY;
        private Vector3 forward = -Vector3.UnitZ;
        private Vector3 right = Vector3.UnitX;

        // --- View Rotations ---
        private float pitch;
        private float yaw = -90f;

        private bool firstMove = true;
        private Vector2 lastPos;

        public Camera(float width, float height, Vector3 position)
        {
            screenWidth = width;
            screenHeight = height;
            this.position = position;
        }

        public Matrix4 getViewMatrix()
        {
            return Matrix4.LookAt(position, position + forward, up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(45.0f), screenWidth/screenHeight, 0.1f, 100f);
        }

        private void UpdateVectors()
        {
            if (pitch > 89f)
            {
                pitch = 89f;
            }
            else if (pitch < -89f)
            {
                pitch = -89f;
            }
                forward.X = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Cos(MathHelper.DegreesToRadians(yaw));
            forward.Y = MathF.Sin(MathHelper.DegreesToRadians(pitch));
            forward.Z = MathF.Cos(MathHelper.DegreesToRadians(pitch)) * MathF.Sin(MathHelper.DegreesToRadians(yaw));

            forward = Vector3.Normalize(forward);

            right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
            up = Vector3.Normalize(Vector3.Cross(right, forward));
        }

        private void InputController(KeyboardState input, MouseState mouse, FrameEventArgs e)
        {
            if (input.IsKeyDown(Keys.W))
            {
                position += forward * speed * (float)e.Time;
            }
            if (input.IsKeyDown(Keys.A))
            {
                position -= right * speed * (float)e.Time;
            }
            if (input.IsKeyDown(Keys.S))
            {
                position -= forward * speed * (float)e.Time;
            }
            if (input.IsKeyDown(Keys.D))
            {
                position += right * speed * (float)e.Time;
            }

            if (input.IsKeyDown(Keys.Space))
            {
                position.Y += speed * (float)e.Time;
            }
            if (input.IsKeyDown(Keys.LeftShift))
            {
                position.Y -= speed * (float)e.Time;
            }

            if (firstMove)
            {
                lastPos = new Vector2(mouse.X, mouse.Y);
                firstMove = false;
            }
            else
            {
                var deltaX = mouse.X - lastPos.X;
                var deltaY = mouse.Y - lastPos.Y;
                lastPos = new Vector2(mouse.X, mouse.Y);

                yaw += deltaX * sensitivity * (float)e.Time;
                pitch -= deltaY * sensitivity * (float)e.Time;
            }

            UpdateVectors();
        }

        public void Update(KeyboardState input, MouseState mouse, FrameEventArgs e)
        {
            InputController(input, mouse, e);
        }
    }
}
