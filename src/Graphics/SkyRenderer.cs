using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.Graphics;

public class SkyRenderer : IDisposable
{
    private readonly ShaderProgram shader;
    private VertexBufferObject? cloudColorVBO;
    private IndexBufferObject? cloudIBO;
    private int cloudIndexCount;

    // Dynamic cloud wind drift
    private Vector2 cloudOffset = Vector2.Zero;
    private VertexBufferObject? cloudUVVBO;

    // Static Cloud Plane Mesh
    private VertexArrayObject? cloudVAO;
    private VertexBufferObject? cloudVBO;
    private VertexBufferObject? quadColorVBO;
    private IndexBufferObject? quadIBO;
    private VertexBufferObject? quadUVVBO;

    // Static Celestial Quad (for Sun and Moon)
    private VertexArrayObject? quadVAO;
    private VertexBufferObject? quadVBO;
    private VertexBufferObject? starsColorVBO;
    private IndexBufferObject? starsIBO;
    private int starsIndexCount;
    private VertexBufferObject? starsUVVBO;

    // Stars Mesh
    private VertexArrayObject? starsVAO;
    private VertexBufferObject? starsVBO;

    public SkyRenderer()
    {
        shader = new ShaderProgram("Sky.vert", "Sky.frag");
        InitCelestialQuad();
        InitCloudPlane();
        InitStars();
    }

    public Vector2 WindVelocity { get; set; } = new(2.5f, 0.8f);
    public float CloudHeight { get; set; } = 112.0f;

    /// <summary>
    ///     Controls the amount/density of clouds in the sky.
    ///     Range: 0.0f (completely clear sky, 0 clouds) to 1.0f (heavy overcast).
    ///     Default is 0.25f for light, sparse, scattered Minecraft-style clouds.
    ///     Edit this variable anytime to easily tune how many clouds appear in the sky.
    /// </summary>
    public float CloudAmount { get; set; } = 0.25f;

    /// <summary>
    ///     Optional direct cutoff threshold in the shader (higher = fewer clouds, lower = denser clouds).
    ///     Set to null to use the intuitive CloudAmount (0.0 to 1.0) scale.
    /// </summary>
    public float? CustomCloudThreshold { get; set; } = null;

    public float CloudFadeStart { get; set; } = 110.0f;
    public float CloudFadeEnd { get; set; } = 240.0f;

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

    private void InitCelestialQuad()
    {
        // Unit square billboard in XY [-1, 1]
        var vertices = new[]
        {
            new Vector3(-1.0f, -1.0f, 0.0f),
            new Vector3(1.0f, -1.0f, 0.0f),
            new Vector3(1.0f, 1.0f, 0.0f),
            new Vector3(-1.0f, 1.0f, 0.0f)
        };

        var uvs = new[]
        {
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 1.0f)
        };

        var colors = new[]
        {
            Vector3.One, Vector3.One, Vector3.One, Vector3.One
        };

        var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

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
        var gridResolution = 24;
        var halfExtent = 260.0f; // Radius 260 blocks around player
        var step = halfExtent * 2.0f / gridResolution;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Vector3>();
        var indices = new List<uint>();

        for (var z = 0; z <= gridResolution; z++)
        {
            var vz = -halfExtent + z * step;
            for (var x = 0; x <= gridResolution; x++)
            {
                var vx = -halfExtent + x * step;
                vertices.Add(new Vector3(vx, 0.0f, vz));
                uvs.Add(new Vector2((float)x / gridResolution, (float)z / gridResolution));
                colors.Add(Vector3.One);
            }
        }

        var vertsPerRow = gridResolution + 1;
        for (var z = 0; z < gridResolution; z++)
        for (var x = 0; x < gridResolution; x++)
        {
            var i0 = (uint)(z * vertsPerRow + x);
            var i1 = (uint)(z * vertsPerRow + x + 1);
            var i2 = (uint)((z + 1) * vertsPerRow + x + 1);
            var i3 = (uint)((z + 1) * vertsPerRow + x);

            indices.Add(i0);
            indices.Add(i1);
            indices.Add(i2);
            indices.Add(i0);
            indices.Add(i2);
            indices.Add(i3);
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
        var starCount = 180;
        var sphereRadius = 185.0f;
        var starHalfSize = 1.1f;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Vector3>();
        var indices = new List<uint>();

        for (var i = 0; i < starCount; i++)
        {
            // Uniform spherical coordinates (upper hemisphere: theta in [0, pi/2])
            var theta = (float)rand.NextDouble() * MathF.PI * 0.45f;
            var phi = (float)rand.NextDouble() * MathF.Tau;

            var sx = sphereRadius * MathF.Sin(theta) * MathF.Cos(phi);
            var sy = sphereRadius * MathF.Cos(theta);
            var sz = sphereRadius * MathF.Sin(theta) * MathF.Sin(phi);
            var starPos = new Vector3(sx, sy, sz);

            var starFwd = Vector3.Normalize(starPos);
            var starUp = MathF.Abs(starFwd.Y) > 0.95f ? Vector3.UnitZ : Vector3.UnitY;
            var starRight = Vector3.Normalize(Vector3.Cross(starUp, starFwd));
            starUp = Vector3.Normalize(Vector3.Cross(starFwd, starRight));

            var baseIdx = (uint)vertices.Count;

            vertices.Add(starPos - starRight * starHalfSize - starUp * starHalfSize);
            vertices.Add(starPos + starRight * starHalfSize - starUp * starHalfSize);
            vertices.Add(starPos + starRight * starHalfSize + starUp * starHalfSize);
            vertices.Add(starPos - starRight * starHalfSize + starUp * starHalfSize);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));

            var brightness = 0.75f + (float)rand.NextDouble() * 0.25f;
            var starCol = new Vector3(brightness, brightness, brightness * 1.05f);
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
    ///     Renders the Sun, Moon, and Stars on the celestial sphere.
    ///     Call this BEFORE drawing terrain with GL.DepthMask(false).
    /// </summary>
    public void RenderCelestials(Camera camera, DayNightCycle? dayNightCycle)
    {
        if (dayNightCycle == null || quadVAO == null || quadIBO == null) return;

        var camPos = camera.Position;
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();

        shader.Bind();

        var viewLoc = GL.GetUniformLocation(shader.ID, "view");
        var projLoc = GL.GetUniformLocation(shader.ID, "projection");
        var modeLoc = GL.GetUniformLocation(shader.ID, "renderMode");
        var colLoc = GL.GetUniformLocation(shader.ID, "celestialColor");
        var rimColLoc = GL.GetUniformLocation(shader.ID, "celestialRimColor");
        var starBrightLoc = GL.GetUniformLocation(shader.ID, "starBrightness");

        GL.UniformMatrix4f(viewLoc, 1, true, ref view);
        GL.UniformMatrix4f(projLoc, 1, true, ref projection);
        var sunHeight = dayNightCycle.SunDirection.Y;
        var starBrightness = Math.Clamp(-sunHeight * 2.5f, 0.0f, 1.0f);

        if (starBrightness > 0.01f && starsVAO != null && starsIBO != null && starsIndexCount > 0)
        {
            var starsModel = Matrix4.CreateTranslation(camPos);
            var modelLoc = GL.GetUniformLocation(shader.ID, "model");
            GL.UniformMatrix4f(modelLoc, 1, true, ref starsModel);

            GL.Uniform1i(modeLoc, 4); // Mode 4 = Stars
            GL.Uniform1f(starBrightLoc, starBrightness);

            starsVAO.Bind();
            starsIBO.Bind();
            GL.DrawElements(PrimitiveType.Triangles, starsIndexCount, DrawElementsType.UnsignedInt, 0);
        }

        var sunDir = dayNightCycle.SunDirection;
        if (sunDir.Y > -0.22f) // Visible above or near horizon
        {
            var sunDist = 180.0f;
            var sunPos = camPos + sunDir * sunDist;
            var sunHalfSize = 14.5f;

            var sunModel = BuildBillboardMatrix(sunPos, sunDir, sunHalfSize);
            var modelLoc = GL.GetUniformLocation(shader.ID, "model");
            GL.UniformMatrix4f(modelLoc, 1, true, ref sunModel);

            GL.Uniform1i(modeLoc, 1); // Mode 1 = Sun

            // Color shifts from warm sunset gold/red at horizon to bright white-gold at zenith
            var heightNorm = Math.Clamp(sunDir.Y, 0.0f, 1.0f);
            var noonSun = new Vector3(1.0f, 0.98f, 0.92f);
            var sunsetSun = new Vector3(1.0f, 0.65f, 0.28f);
            var sunCol = Vector3.Lerp(sunsetSun, noonSun, MathF.Pow(heightNorm, 0.5f));

            var noonRim = new Vector3(1.0f, 0.85f, 0.32f);
            var sunsetRim = new Vector3(0.95f, 0.38f, 0.12f);
            var sunRim = Vector3.Lerp(sunsetRim, noonRim, MathF.Pow(heightNorm, 0.5f));

            GL.Uniform3f(colLoc, 1, ref sunCol);
            GL.Uniform3f(rimColLoc, 1, ref sunRim);

            quadVAO.Bind();
            quadIBO.Bind();
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }

        var moonDir = dayNightCycle.MoonDirection;
        if (moonDir.Y > -0.22f) // Visible above or near horizon
        {
            var moonDist = 180.0f;
            var moonPos = camPos + moonDir * moonDist;
            var moonHalfSize = 12.5f;

            var moonModel = BuildBillboardMatrix(moonPos, moonDir, moonHalfSize);
            var modelLoc = GL.GetUniformLocation(shader.ID, "model");
            GL.UniformMatrix4f(modelLoc, 1, true, ref moonModel);

            GL.Uniform1i(modeLoc, 2); // Mode 2 = Moon

            var moonCol = new Vector3(0.92f, 0.95f, 1.0f);
            var moonRim = new Vector3(0.55f, 0.65f, 0.85f);

            GL.Uniform3f(colLoc, 1, ref moonCol);
            GL.Uniform3f(rimColLoc, 1, ref moonRim);

            quadVAO.Bind();
            quadIBO.Bind();
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
        }
    }

    /// <summary>
    ///     Renders the dynamic drifting voxel clouds overhead.
    ///     Call this after terrain has been rendered.
    /// </summary>
    public void RenderClouds(Camera camera, DayNightCycle? dayNightCycle)
    {
        if (cloudVAO == null || cloudIBO == null || cloudIndexCount == 0) return;

        var camPos = camera.Position;
        var view = camera.GetViewMatrix();
        var projection = camera.GetProjectionMatrix();

        shader.Bind();

        var modelLoc = GL.GetUniformLocation(shader.ID, "model");
        var viewLoc = GL.GetUniformLocation(shader.ID, "view");
        var projLoc = GL.GetUniformLocation(shader.ID, "projection");
        var modeLoc = GL.GetUniformLocation(shader.ID, "renderMode");
        var viewPosLoc = GL.GetUniformLocation(shader.ID, "viewPos");
        var offsetLoc = GL.GetUniformLocation(shader.ID, "cloudOffset");
        var cloudColLoc = GL.GetUniformLocation(shader.ID, "cloudColor");
        var cloudAlphaLoc = GL.GetUniformLocation(shader.ID, "cloudAlpha");
        var fadeStartLoc = GL.GetUniformLocation(shader.ID, "fadeStart");
        var fadeEndLoc = GL.GetUniformLocation(shader.ID, "fadeEnd");

        GL.UniformMatrix4f(viewLoc, 1, true, ref view);
        GL.UniformMatrix4f(projLoc, 1, true, ref projection);
        GL.Uniform3f(viewPosLoc, 1, ref camPos);

        // Cloud plane follows camera in XZ so it feels infinite
        var planeX = MathF.Floor(camPos.X);
        var planeZ = MathF.Floor(camPos.Z);
        var cloudModel = Matrix4.CreateTranslation(new Vector3(planeX, CloudHeight, planeZ));
        GL.UniformMatrix4f(modelLoc, 1, true, ref cloudModel);

        GL.Uniform1i(modeLoc, 3); // Mode 3 = Dynamic Clouds
        GL.Uniform2f(offsetLoc, 1, ref cloudOffset);

        // Compute cloud color matching time of day
        var cloudColor = ComputeCloudColor(dayNightCycle);
        var cloudAlpha = 0.88f;
        var fadeStart = CloudFadeStart;
        var fadeEnd = CloudFadeEnd;

        GL.Uniform3f(cloudColLoc, 1, ref cloudColor);
        GL.Uniform1f(cloudAlphaLoc, cloudAlpha);
        GL.Uniform1f(fadeStartLoc, fadeStart);
        GL.Uniform1f(fadeEndLoc, fadeEnd);

        // Cloud threshold (higher = fewer clouds, lower = denser clouds)
        var thresholdLoc = GL.GetUniformLocation(shader.ID, "cloudThreshold");
        var threshold = CustomCloudThreshold ?? MathHelper.Lerp(0.85f, 0.30f, Math.Clamp(CloudAmount, 0.0f, 1.0f));
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
            DayNightPhase.Dawn => new Vector3(1.0f, 0.72f, 0.52f), // Rosy sunrise amber
            DayNightPhase.Day => new Vector3(1.0f, 1.0f, 1.0f), // Crisp brilliant white
            DayNightPhase.Dusk => new Vector3(0.95f, 0.48f, 0.32f), // Fiery sunset orange
            DayNightPhase.Night => new Vector3(0.18f, 0.20f, 0.28f), // Midnight lunar indigo
            _ => Vector3.One
        };
    }

    private static Matrix4 BuildBillboardMatrix(Vector3 position, Vector3 direction, float halfSize)
    {
        var forward = Vector3.Normalize(direction);
        var tempUp = MathF.Abs(forward.Y) > 0.95f ? Vector3.UnitZ : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(tempUp, forward));
        var up = Vector3.Normalize(Vector3.Cross(forward, right));

        // Row-major matrix matching OpenTK matrix-vector convention
        return new Matrix4(
            new Vector4(right * halfSize),
            new Vector4(up * halfSize),
            new Vector4(forward * halfSize),
            new Vector4(position, 1.0f)
        );
    }
}