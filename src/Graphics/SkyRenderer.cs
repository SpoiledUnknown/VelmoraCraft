using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.Graphics
{
    public class SkyRenderer : IDisposable
    {
        private readonly ShaderProgram shader;

        // Static Celestial Quad (for Sun and Moon)
        private VertexArrayObject? quadVAO;
        private VertexBufferObject? quadVBO;
        private VertexBufferObject? quadUVVBO;
        private VertexBufferObject? quadColorVBO;
        private IndexBufferObject? quadIBO;

        // Static Cloud Plane Mesh
        private VertexArrayObject? cloudVAO;
        private VertexBufferObject? cloudVBO;
        private VertexBufferObject? cloudUVVBO;
        private VertexBufferObject? cloudColorVBO;
        private IndexBufferObject? cloudIBO;
        private int cloudIndexCount = 0;

        // Stars Mesh
        private VertexArrayObject? starsVAO;
        private VertexBufferObject? starsVBO;
        private VertexBufferObject? starsUVVBO;
        private VertexBufferObject? starsColorVBO;
        private IndexBufferObject? starsIBO;
        private int starsIndexCount = 0;

        // Dynamic cloud wind drift
        private Vector2 cloudOffset = Vector2.Zero;
        public Vector2 WindVelocity { get; set; } = new Vector2(2.5f, 0.8f);
        public float CloudHeight { get; set; } = 112.0f;

        /// <summary>
        /// Controls the amount/density of clouds in the sky.
        /// Range: 0.0f (completely clear sky, 0 clouds) to 1.0f (heavy overcast).
        /// Default is 0.25f for light, sparse, scattered Minecraft-style clouds.
        /// Edit this variable anytime to easily tune how many clouds appear in the sky.
        /// </summary>
        public float CloudAmount { get; set; } = 0.25f;

        /// <summary>
        /// Optional direct cutoff threshold in the shader (higher = fewer clouds, lower = denser clouds).
        /// Set to null to use the intuitive CloudAmount (0.0 to 1.0) scale.
        /// </summary>
        public float? CustomCloudThreshold { get; set; } = null;

        public float CloudFadeStart { get; set; } = 110.0f;
        public float CloudFadeEnd { get; set; } = 240.0f;

        public SkyRenderer()
        {
            shader = new ShaderProgram("Sky.vert", "Sky.frag");
            InitCelestialQuad();
            InitCloudPlane();
            InitStars();
        }

        private void InitCelestialQuad()
        {
            // Unit square billboard in XY [-1, 1]
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3( 1.0f, -1.0f, 0.0f),
                new Vector3( 1.0f,  1.0f, 0.0f),
                new Vector3(-1.0f,  1.0f, 0.0f)
            };

            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f)
            };

            Vector3[] colors = new Vector3[]
            {
                Vector3.One, Vector3.One, Vector3.One, Vector3.One
            };

            uint[] indices = new uint[] { 0, 1, 2, 0, 2, 3 };

            quadVAO = new VertexArrayObject();
            quadVAO.Bind();

            quadVBO = new VertexBufferObject(vertices.AsSpan());
            quadVAO.LinkToVAO(0, 3, quadVBO);

            quadUVVBO = new VertexBufferObject(uvs.AsSpan());
            quadVAO.LinkToVAO(1, 2, quadUVVBO);

            quadColorVBO = new VertexBufferObject(colors.AsSpan());
            quadVAO.LinkToVAO(2, 3, quadColorVBO);

            quadIBO = new IndexBufferObject(indices.AsSpan());
        }

        private void InitCloudPlane()
        {
            // Subdivided horizontal grid for clouds
            int gridResolution = 24;
            float halfExtent = 260.0f; // Radius 260 blocks around player
            float step = (halfExtent * 2.0f) / gridResolution;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Vector3>();
            var indices = new List<uint>();

            for (int z = 0; z <= gridResolution; z++)
            {
                float vz = -halfExtent + z * step;
                for (int x = 0; x <= gridResolution; x++)
                {
                    float vx = -halfExtent + x * step;
                    vertices.Add(new Vector3(vx, 0.0f, vz));
                    uvs.Add(new Vector2((float)x / gridResolution, (float)z / gridResolution));
                    colors.Add(Vector3.One);
                }
            }

            int vertsPerRow = gridResolution + 1;
            for (int z = 0; z < gridResolution; z++)
            {
                for (int x = 0; x < gridResolution; x++)
                {
                    uint i0 = (uint)(z * vertsPerRow + x);
                    uint i1 = (uint)(z * vertsPerRow + (x + 1));
                    uint i2 = (uint)((z + 1) * vertsPerRow + (x + 1));
                    uint i3 = (uint)((z + 1) * vertsPerRow + x);

                    indices.Add(i0);
                    indices.Add(i1);
                    indices.Add(i2);
                    indices.Add(i0);
                    indices.Add(i2);
                    indices.Add(i3);
                }
            }

            cloudIndexCount = indices.Count;

            cloudVAO = new VertexArrayObject();
            cloudVAO.Bind();

            cloudVBO = new VertexBufferObject(vertices.ToArray().AsSpan());
            cloudVAO.LinkToVAO(0, 3, cloudVBO);

            cloudUVVBO = new VertexBufferObject(uvs.ToArray().AsSpan());
            cloudVAO.LinkToVAO(1, 2, cloudUVVBO);

            cloudColorVBO = new VertexBufferObject(colors.ToArray().AsSpan());
            cloudVAO.LinkToVAO(2, 3, cloudColorVBO);

            cloudIBO = new IndexBufferObject(indices.ToArray().AsSpan());
        }

        private void InitStars()
        {
            // Generate deterministic twinkling stars distributed across upper celestial dome
            var rand = new Random(42);
            int starCount = 180;
            float sphereRadius = 185.0f;
            float starHalfSize = 1.1f;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Vector3>();
            var indices = new List<uint>();

            for (int i = 0; i < starCount; i++)
            {
                // Uniform spherical coordinates (upper hemisphere: theta in [0, pi/2])
                float theta = (float)rand.NextDouble() * MathF.PI * 0.45f;
                float phi = (float)rand.NextDouble() * MathF.Tau;

                float sx = sphereRadius * MathF.Sin(theta) * MathF.Cos(phi);
                float sy = sphereRadius * MathF.Cos(theta);
                float sz = sphereRadius * MathF.Sin(theta) * MathF.Sin(phi);
                Vector3 starPos = new Vector3(sx, sy, sz);

                Vector3 starFwd = Vector3.Normalize(starPos);
                Vector3 starUp = MathF.Abs(starFwd.Y) > 0.95f ? Vector3.UnitZ : Vector3.UnitY;
                Vector3 starRight = Vector3.Normalize(Vector3.Cross(starUp, starFwd));
                starUp = Vector3.Normalize(Vector3.Cross(starFwd, starRight));

                uint baseIdx = (uint)vertices.Count;

                vertices.Add(starPos - starRight * starHalfSize - starUp * starHalfSize);
                vertices.Add(starPos + starRight * starHalfSize - starUp * starHalfSize);
                vertices.Add(starPos + starRight * starHalfSize + starUp * starHalfSize);
                vertices.Add(starPos - starRight * starHalfSize + starUp * starHalfSize);

                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));

                float brightness = 0.75f + (float)rand.NextDouble() * 0.25f;
                Vector3 starCol = new Vector3(brightness, brightness, brightness * 1.05f);
                colors.Add(starCol);
                colors.Add(starCol);
                colors.Add(starCol);
                colors.Add(starCol);

                indices.Add(baseIdx + 0);
                indices.Add(baseIdx + 1);
                indices.Add(baseIdx + 2);
                indices.Add(baseIdx + 0);
                indices.Add(baseIdx + 2);
                indices.Add(baseIdx + 3);
            }

            starsIndexCount = indices.Count;

            starsVAO = new VertexArrayObject();
            starsVAO.Bind();

            starsVBO = new VertexBufferObject(vertices.ToArray().AsSpan());
            starsVAO.LinkToVAO(0, 3, starsVBO);

            starsUVVBO = new VertexBufferObject(uvs.ToArray().AsSpan());
            starsVAO.LinkToVAO(1, 2, starsUVVBO);

            starsColorVBO = new VertexBufferObject(colors.ToArray().AsSpan());
            starsVAO.LinkToVAO(2, 3, starsColorVBO);

            starsIBO = new IndexBufferObject(indices.ToArray().AsSpan());
        }

        public void Update(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                // Continuously drift clouds across the sky
                cloudOffset += WindVelocity * deltaTime;
                if (cloudOffset.X > 10000f) cloudOffset.X -= 10000f;
                if (cloudOffset.Y > 10000f) cloudOffset.Y -= 10000f;
            }
        }

        /// <summary>
        /// Renders the Sun, Moon, and Stars on the celestial sphere.
        /// Call this BEFORE drawing terrain with GL.DepthMask(false).
        /// </summary>
        public void RenderCelestials(Camera camera, DayNightCycle? dayNightCycle)
        {
            if (dayNightCycle == null || quadVAO == null || quadIBO == null) return;

            Vector3 camPos = camera.Position;
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            shader.Bind();

            int viewLoc = GL.GetUniformLocation(shader.ID, "view");
            int projLoc = GL.GetUniformLocation(shader.ID, "projection");
            int modeLoc = GL.GetUniformLocation(shader.ID, "renderMode");
            int colLoc = GL.GetUniformLocation(shader.ID, "celestialColor");
            int rimColLoc = GL.GetUniformLocation(shader.ID, "celestialRimColor");
            int starBrightLoc = GL.GetUniformLocation(shader.ID, "starBrightness");

            GL.UniformMatrix4f(viewLoc, 1, true, ref view);
            GL.UniformMatrix4f(projLoc, 1, true, ref projection);

            float sunHeight = dayNightCycle.SunDirection.Y;
            float starBrightness = Math.Clamp(-sunHeight * 2.5f, 0.0f, 1.0f);

            if (starBrightness > 0.01f && starsVAO != null && starsIBO != null && starsIndexCount > 0)
            {
                Matrix4 starsModel = Matrix4.CreateTranslation(camPos);
                int modelLoc = GL.GetUniformLocation(shader.ID, "model");
                GL.UniformMatrix4f(modelLoc, 1, true, ref starsModel);

                GL.Uniform1i(modeLoc, 4); // Mode 4 = Stars
                GL.Uniform1f(starBrightLoc, starBrightness);

                starsVAO.Bind();
                starsIBO.Bind();
                GL.DrawElements(PrimitiveType.Triangles, starsIndexCount, DrawElementsType.UnsignedInt, 0);
            }

            Vector3 sunDir = dayNightCycle.SunDirection;
            if (sunDir.Y > -0.22f) // Visible above or near horizon
            {
                float sunDist = 180.0f;
                Vector3 sunPos = camPos + sunDir * sunDist;
                float sunHalfSize = 14.5f;

                Matrix4 sunModel = BuildBillboardMatrix(sunPos, sunDir, sunHalfSize);
                int modelLoc = GL.GetUniformLocation(shader.ID, "model");
                GL.UniformMatrix4f(modelLoc, 1, true, ref sunModel);

                GL.Uniform1i(modeLoc, 1); // Mode 1 = Sun

                // Color shifts from warm sunset gold/red at horizon to bright white-gold at zenith
                float heightNorm = Math.Clamp(sunDir.Y, 0.0f, 1.0f);
                Vector3 noonSun = new Vector3(1.0f, 0.98f, 0.92f);
                Vector3 sunsetSun = new Vector3(1.0f, 0.65f, 0.28f);
                Vector3 sunCol = Vector3.Lerp(sunsetSun, noonSun, MathF.Pow(heightNorm, 0.5f));

                Vector3 noonRim = new Vector3(1.0f, 0.85f, 0.32f);
                Vector3 sunsetRim = new Vector3(0.95f, 0.38f, 0.12f);
                Vector3 sunRim = Vector3.Lerp(sunsetRim, noonRim, MathF.Pow(heightNorm, 0.5f));

                GL.Uniform3f(colLoc, 1, ref sunCol);
                GL.Uniform3f(rimColLoc, 1, ref sunRim);

                quadVAO.Bind();
                quadIBO.Bind();
                GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            }

            Vector3 moonDir = dayNightCycle.MoonDirection;
            if (moonDir.Y > -0.22f) // Visible above or near horizon
            {
                float moonDist = 180.0f;
                Vector3 moonPos = camPos + moonDir * moonDist;
                float moonHalfSize = 12.5f;

                Matrix4 moonModel = BuildBillboardMatrix(moonPos, moonDir, moonHalfSize);
                int modelLoc = GL.GetUniformLocation(shader.ID, "model");
                GL.UniformMatrix4f(modelLoc, 1, true, ref moonModel);

                GL.Uniform1i(modeLoc, 2); // Mode 2 = Moon

                Vector3 moonCol = new Vector3(0.92f, 0.95f, 1.0f);
                Vector3 moonRim = new Vector3(0.55f, 0.65f, 0.85f);

                GL.Uniform3f(colLoc, 1, ref moonCol);
                GL.Uniform3f(rimColLoc, 1, ref moonRim);

                quadVAO.Bind();
                quadIBO.Bind();
                GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
            }
        }

        /// <summary>
        /// Renders the dynamic drifting voxel clouds overhead.
        /// Call this after terrain has been rendered.
        /// </summary>
        public void RenderClouds(Camera camera, DayNightCycle? dayNightCycle)
        {
            if (cloudVAO == null || cloudIBO == null || cloudIndexCount == 0) return;

            Vector3 camPos = camera.Position;
            Matrix4 view = camera.GetViewMatrix();
            Matrix4 projection = camera.GetProjectionMatrix();

            shader.Bind();

            int modelLoc = GL.GetUniformLocation(shader.ID, "model");
            int viewLoc = GL.GetUniformLocation(shader.ID, "view");
            int projLoc = GL.GetUniformLocation(shader.ID, "projection");
            int modeLoc = GL.GetUniformLocation(shader.ID, "renderMode");
            int viewPosLoc = GL.GetUniformLocation(shader.ID, "viewPos");
            int offsetLoc = GL.GetUniformLocation(shader.ID, "cloudOffset");
            int cloudColLoc = GL.GetUniformLocation(shader.ID, "cloudColor");
            int cloudAlphaLoc = GL.GetUniformLocation(shader.ID, "cloudAlpha");
            int fadeStartLoc = GL.GetUniformLocation(shader.ID, "fadeStart");
            int fadeEndLoc = GL.GetUniformLocation(shader.ID, "fadeEnd");

            GL.UniformMatrix4f(viewLoc, 1, true, ref view);
            GL.UniformMatrix4f(projLoc, 1, true, ref projection);
            GL.Uniform3f(viewPosLoc, 1, ref camPos);

            // Cloud plane follows camera in XZ so it feels infinite
            float planeX = MathF.Floor(camPos.X);
            float planeZ = MathF.Floor(camPos.Z);
            Matrix4 cloudModel = Matrix4.CreateTranslation(new Vector3(planeX, CloudHeight, planeZ));
            GL.UniformMatrix4f(modelLoc, 1, true, ref cloudModel);

            GL.Uniform1i(modeLoc, 3); // Mode 3 = Dynamic Clouds
            GL.Uniform2f(offsetLoc, 1, ref cloudOffset);

            // Compute cloud color matching time of day
            Vector3 cloudColor = ComputeCloudColor(dayNightCycle);
            float cloudAlpha = 0.88f;
            float fadeStart = CloudFadeStart;
            float fadeEnd = CloudFadeEnd;

            GL.Uniform3f(cloudColLoc, 1, ref cloudColor);
            GL.Uniform1f(cloudAlphaLoc, cloudAlpha);
            GL.Uniform1f(fadeStartLoc, fadeStart);
            GL.Uniform1f(fadeEndLoc, fadeEnd);

            // Cloud threshold (higher = fewer clouds, lower = denser clouds)
            int thresholdLoc = GL.GetUniformLocation(shader.ID, "cloudThreshold");
            float threshold = CustomCloudThreshold ?? MathHelper.Lerp(0.85f, 0.30f, Math.Clamp(CloudAmount, 0.0f, 1.0f));
            GL.Uniform1f(thresholdLoc, threshold);

            cloudVAO.Bind();
            cloudIBO.Bind();
            GL.DrawElements(PrimitiveType.Triangles, cloudIndexCount, DrawElementsType.UnsignedInt, 0);
        }

        private static Vector3 ComputeCloudColor(DayNightCycle? cycle)
        {
            if (cycle == null) return Vector3.One;

            return cycle.CurrentPhase switch
            {
                DayNightPhase.Dawn => new Vector3(1.0f, 0.72f, 0.52f),  // Rosy sunrise amber
                DayNightPhase.Day  => new Vector3(1.0f, 1.0f, 1.0f),    // Crisp brilliant white
                DayNightPhase.Dusk => new Vector3(0.95f, 0.48f, 0.32f), // Fiery sunset orange
                DayNightPhase.Night => new Vector3(0.18f, 0.20f, 0.28f),// Midnight lunar indigo
                _ => Vector3.One
            };
        }

        private static Matrix4 BuildBillboardMatrix(Vector3 position, Vector3 direction, float halfSize)
        {
            Vector3 forward = Vector3.Normalize(direction);
            Vector3 tempUp = MathF.Abs(forward.Y) > 0.95f ? Vector3.UnitZ : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(tempUp, forward));
            Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

            // Row-major matrix matching OpenTK matrix-vector convention
            return new Matrix4(
                new Vector4(right * halfSize, 0.0f),
                new Vector4(up * halfSize, 0.0f),
                new Vector4(forward * halfSize, 0.0f),
                new Vector4(position, 1.0f)
            );
        }

        public void Dispose()
        {
            quadVAO?.Delete();
            quadVBO?.Delete();
            quadUVVBO?.Delete();
            quadColorVBO?.Delete();
            quadIBO?.Delete();

            cloudVAO?.Delete();
            cloudVBO?.Delete();
            cloudUVVBO?.Delete();
            cloudColorVBO?.Delete();
            cloudIBO?.Delete();

            starsVAO?.Delete();
            starsVBO?.Delete();
            starsUVVBO?.Delete();
            starsColorVBO?.Delete();
            starsIBO?.Delete();

            shader?.Delete();
        }
    }
}
