using OpenTK.Mathematics;

namespace VoxelRenderEngine.src
{
    public class Camera
    {
        private float fov = 60.0f;
        private float aspectRatio;
        private float nearPlane = 0.1f;
        private float farPlane = 300.0f;

        public Vector3 Position { get; set; }
        public float Pitch { get; set; } = 0.0f;
        public float Yaw { get; set; } = -90.0f;

        public Vector3 Forward { get; private set; } = -Vector3.UnitZ;
        public Vector3 Right { get; private set; } = Vector3.UnitX;
        public Vector3 Up { get; private set; } = Vector3.UnitY;

        public Camera(float width, float height, Vector3 position)
        {
            aspectRatio = width / Math.Max(1.0f, height);
            Position = position;
            UpdateVectors();
        }

        public void UpdateAspectRatio(float width, float height)
        {
            aspectRatio = width / Math.Max(1.0f, height);
        }

        public Matrix4 GetViewMatrix()
        {
            return Matrix4.LookAt(Position, Position + Forward, Up);
        }

        public Matrix4 GetProjectionMatrix()
        {
            return Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(fov),
                aspectRatio,
                nearPlane,
                farPlane
            );
        }

        public void Rotate(float deltaYaw, float deltaPitch)
        {
            Yaw += deltaYaw;
            Pitch = Math.Clamp(Pitch + deltaPitch, -89.0f, 89.0f);
            UpdateVectors();
        }

        public void UpdateVectors()
        {
            float pitchRad = MathHelper.DegreesToRadians(Pitch);
            float yawRad = MathHelper.DegreesToRadians(Yaw);

            Vector3 newForward;
            newForward.X = MathF.Cos(pitchRad) * MathF.Cos(yawRad);
            newForward.Y = MathF.Sin(pitchRad);
            newForward.Z = MathF.Cos(pitchRad) * MathF.Sin(yawRad);

            Forward = Vector3.Normalize(newForward);
            Right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitY));
            Up = Vector3.Normalize(Vector3.Cross(Right, Forward));
        }
    }
}
