using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src
{
    public class FreeFlyCameraController
    {
        public Camera Camera { get; }

        public float MoveSpeed { get; set; } = 15.0f;
        public float SprintMultiplier { get; set; } = 2.5f;
        public float MouseSensitivity { get; set; } = 0.15f;

        public bool IsEnabled { get; set; } = true;

        public FreeFlyCameraController(Camera camera)
        {
            Camera = camera;
        }

        public void ProcessMouseMovement(Vector2 mouseDelta)
        {
            if (!IsEnabled) return;

            // Invert Y for standard FPS mouse look
            float xOffset = mouseDelta.X * MouseSensitivity;
            float yOffset = -mouseDelta.Y * MouseSensitivity;

            Camera.Rotate(xOffset, yOffset);
        }

        public void Update(KeyboardState keyboard, float deltaTime)
        {
            if (!IsEnabled) return;

            float currentSpeed = MoveSpeed;
            if (keyboard.IsKeyDown(Keys.LeftControl))
            {
                currentSpeed *= SprintMultiplier;
            }

            Vector3 moveDirection = Vector3.Zero;

            // Forward/Back (relative to camera facing)
            if (keyboard.IsKeyDown(Keys.W)) moveDirection += Camera.Forward;
            if (keyboard.IsKeyDown(Keys.S)) moveDirection -= Camera.Forward;

            // Strafe Left/Right
            if (keyboard.IsKeyDown(Keys.D)) moveDirection += Camera.Right;
            if (keyboard.IsKeyDown(Keys.A)) moveDirection -= Camera.Right;

            // Ascend/Descend (Fly vertical)
            if (keyboard.IsKeyDown(Keys.Space)) moveDirection += Vector3.UnitY;
            if (keyboard.IsKeyDown(Keys.LeftShift)) moveDirection -= Vector3.UnitY;

            if (moveDirection.LengthSquared > 0.0001f)
            {
                moveDirection = Vector3.Normalize(moveDirection);
                Camera.Position += moveDirection * currentSpeed * deltaTime;
            }

            // Keep within the world boundary [1, 127]
            Vector3 pos = Camera.Position;
            pos.X = Math.Clamp(pos.X, 1.0f, (float)World.World.WORLD_SIZE_X - 1.0f);
            pos.Z = Math.Clamp(pos.Z, 1.0f, (float)World.World.WORLD_SIZE_Z - 1.0f);
            pos.Y = Math.Clamp(pos.Y, 1.0f, 125.0f);
            Camera.Position = pos;
        }
    }
}
