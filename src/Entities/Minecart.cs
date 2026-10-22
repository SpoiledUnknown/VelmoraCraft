using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VelmoraCraft.src.Graphics;
using VelmoraCraft.src.Physics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.Entities;

public class Minecart
{
    public const float WIDTH = 0.75f;
    public const float LENGTH = 0.95f;
    public const float HEIGHT = 0.55f;

    public Minecart(Vector3 spawnPosition)
    {
        Position = spawnPosition;
    }

    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; } = Vector3.Zero;
    public float RotationY { get; set; } // Yaw angle in radians
    public Player? Rider { get; set; }
    public bool HasRider => Rider != null;
    public bool IsAlive { get; set; } = true;

    public AABB GetAABB()
    {
        var halfW = WIDTH * 0.5f;
        var halfL = LENGTH * 0.5f;
        return new AABB(
            Position.X - halfW, Position.Y, Position.Z - halfL,
            Position.X + halfW, Position.Y + HEIGHT, Position.Z + halfL
        );
    }

    public void ApplyPush(Vector3 force)
    {
        Velocity += force;
        var horizontalSpeed = MathF.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
        if (horizontalSpeed > 11.0f)
        {
            var factor = 11.0f / horizontalSpeed;
            Velocity = new Vector3(Velocity.X * factor, Velocity.Y, Velocity.Z * factor);
        }
    }

    public void Mount(Player player)
    {
        Rider = player;
    }

    public void Dismount()
    {
        Rider = null;
    }

    public void Update(World.World world, float deltaTime)
    {
        var rx = (int)MathF.Round(Position.X);
        var ry = (int)MathF.Floor(Position.Y + 0.15f);
        var rz = (int)MathF.Round(Position.Z);

        var trackY = ry;
        var blockUnder = world.GetBlockWorld(rx, trackY, rz);
        var onRail = IsRailBlock(blockUnder);

        if (!onRail)
        {
            trackY = ry - 1;
            blockUnder = world.GetBlockWorld(rx, trackY, rz);
            onRail = IsRailBlock(blockUnder);
        }

        if (onRail)
        {
            // Snap height to sit cleanly on the rail
            var railSurfaceY = trackY + 0.03f;
            Position = new Vector3(Position.X, railSurfaceY, Position.Z);
            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);

            // Detect track direction
            var hasRailX = IsRailBlock(world.GetBlockWorld(rx + 1, trackY, rz)) ||
                           IsRailBlock(world.GetBlockWorld(rx - 1, trackY, rz));
            var hasRailZ = IsRailBlock(world.GetBlockWorld(rx, trackY, rz + 1)) ||
                           IsRailBlock(world.GetBlockWorld(rx, trackY, rz - 1));

            var absVx = MathF.Abs(Velocity.X);
            var absVz = MathF.Abs(Velocity.Z);

            if (hasRailX && !hasRailZ)
            {
                // Track strictly along X: center Z
                Position = new Vector3(Position.X, Position.Y, MathHelper.Lerp(Position.Z, rz, 14.0f * deltaTime));
                Velocity = new Vector3(Velocity.X, 0, 0);
            }
            else if (hasRailZ && !hasRailX)
            {
                // Track strictly along Z: center X
                Position = new Vector3(MathHelper.Lerp(Position.X, rx, 14.0f * deltaTime), Position.Y, Position.Z);
                Velocity = new Vector3(0, 0, Velocity.Z);
            }
            else
            {
                // Intersection or open track: align non-dominant axis
                if (absVx > absVz)
                    Position = new Vector3(Position.X, Position.Y, MathHelper.Lerp(Position.Z, rz, 10.0f * deltaTime));
                else if (absVz > absVx)
                    Position = new Vector3(MathHelper.Lerp(Position.X, rx, 10.0f * deltaTime), Position.Y, Position.Z);
            }

            // Power rails behavior
            if (blockUnder == (byte)BlockType.PoweredRailOn)
            {
                var speed = Velocity.Length;
                Vector3 forwardDir;

                if (speed > 0.1f)
                {
                    forwardDir = Vector3.Normalize(Velocity);
                }
                else if (HasRider && Rider != null)
                {
                    forwardDir = new Vector3(Rider.Camera.Forward.X, 0, Rider.Camera.Forward.Z);
                    if (forwardDir.LengthSquared > 0.01f) forwardDir = Vector3.Normalize(forwardDir);
                    else forwardDir = Vector3.UnitX;
                }
                else
                {
                    forwardDir = Vector3.UnitX;
                }

                var boostedSpeed = MathF.Min(11.5f, MathF.Max(speed + 15.0f * deltaTime, 4.5f));
                Velocity = forwardDir * boostedSpeed;
            }
            else if (blockUnder == (byte)BlockType.PoweredRail)
            {
                // Unpowered rail brakes
                Velocity *= MathF.Pow(0.12f, deltaTime);
            }
            else
            {
                // Regular rail rolling friction
                Velocity *= MathF.Pow(0.96f, deltaTime);
            }

            // Update facing rotation
            if (Velocity.X * Velocity.X + Velocity.Z * Velocity.Z > 0.05f)
                RotationY = MathF.Atan2(Velocity.X, Velocity.Z);
        }
        else
        {
            // Off-rail physics: gravity and floor collision
            Velocity = new Vector3(Velocity.X, Velocity.Y - 22.0f * deltaTime, Velocity.Z);
            var checkFloorY = (int)MathF.Floor(Position.Y + Velocity.Y * deltaTime);

            if (BlockRegistry.IsSolid(world.GetBlockWorld(rx, checkFloorY, rz)))
            {
                Position = new Vector3(Position.X, checkFloorY + 1.0f, Position.Z);
                Velocity = new Vector3(Velocity.X * 0.7f, 0, Velocity.Z * 0.7f);
            }

            // Rough ground friction
            Velocity = new Vector3(Velocity.X * MathF.Pow(0.2f, deltaTime), Velocity.Y,
                Velocity.Z * MathF.Pow(0.2f, deltaTime));
        }

        Position += Velocity * deltaTime;

        // Clamp inside world bounds
        Position = new Vector3(
            Math.Clamp(Position.X, 1.0f, World.World.WORLD_SIZE_X - 1.0f),
            Math.Clamp(Position.Y, 1.0f, 125.0f),
            Math.Clamp(Position.Z, 1.0f, World.World.WORLD_SIZE_Z - 1.0f)
        );
    }

    private static bool IsRailBlock(byte blockId)
    {
        return blockId == (byte)BlockType.Rail ||
               blockId == (byte)BlockType.PoweredRail ||
               blockId == (byte)BlockType.PoweredRailOn;
    }
}

public static class MinecartRenderer
{
    private static VertexArrayObject? vao;
    private static VertexBufferObject? vertexVbo;
    private static VertexBufferObject? uvVbo;
    private static VertexBufferObject? colorVbo;
    private static VertexBufferObject? normalVbo;
    private static IndexBufferObject? ibo;
    private static int indexCount;
    private static bool isInitialized;
    private static Vector2 cachedUV = new(-1, -1);
    private static Vector3 cachedTint = Vector3.Zero;

    public static void Initialize()
    {
        if (isInitialized) return;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colors = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<uint>();
        uint vertexOffset = 0;

        // Dynamically read texture atlas coordinates & tint from BlockRegistry
        var def = BlockRegistry.Get(BlockType.MinecartItem);
        var atlasUV = def.GetFaceAtlasCoord(Face.Front);
        var tint = def.TintColor;
        cachedUV = atlasUV;
        cachedTint = tint;
        var atlasSize = 16f;

        var u0 = atlasUV.X / atlasSize;
        var u1 = (atlasUV.X + 1f) / atlasSize;
        var b0 = 1.0f - (atlasUV.Y + 1f) / atlasSize;
        var b1 = 1.0f - atlasUV.Y / atlasSize;

        var hw = Minecart.WIDTH * 0.5f; // 0.375
        var hl = Minecart.LENGTH * 0.5f; // 0.475
        var h = Minecart.HEIGHT; // 0.55

        // Helper to add double-sided quads
        void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 norm)
        {
            // Front face
            vertices.Add(v0);
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            for (var i = 0; i < 4; i++)
            {
                colors.Add(tint);
                normals.Add(norm);
            }

            uvs.Add(new Vector2(u0, b1));
            uvs.Add(new Vector2(u1, b1));
            uvs.Add(new Vector2(u1, b0));
            uvs.Add(new Vector2(u0, b0));

            indices.Add(0 + vertexOffset);
            indices.Add(1 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(3 + vertexOffset);
            indices.Add(0 + vertexOffset);
            vertexOffset += 4;

            // Back face
            vertices.Add(v1);
            vertices.Add(v0);
            vertices.Add(v3);
            vertices.Add(v2);
            for (var i = 0; i < 4; i++)
            {
                colors.Add(tint);
                normals.Add(-norm);
            }

            uvs.Add(new Vector2(u1, b1));
            uvs.Add(new Vector2(u0, b1));
            uvs.Add(new Vector2(u0, b0));
            uvs.Add(new Vector2(u1, b0));

            indices.Add(0 + vertexOffset);
            indices.Add(1 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(2 + vertexOffset);
            indices.Add(3 + vertexOffset);
            indices.Add(0 + vertexOffset);
            vertexOffset += 4;
        }

        AddQuad(
            new Vector3(-hw, 0.04f, -hl),
            new Vector3(hw, 0.04f, -hl),
            new Vector3(hw, 0.04f, hl),
            new Vector3(-hw, 0.04f, hl),
            Vector3.UnitY
        );

        AddQuad(
            new Vector3(-hw, h, hl),
            new Vector3(hw, h, hl),
            new Vector3(hw, 0.04f, hl),
            new Vector3(-hw, 0.04f, hl),
            Vector3.UnitZ
        );

        AddQuad(
            new Vector3(hw, h, -hl),
            new Vector3(-hw, h, -hl),
            new Vector3(-hw, 0.04f, -hl),
            new Vector3(hw, 0.04f, -hl),
            -Vector3.UnitZ
        );

        AddQuad(
            new Vector3(-hw, h, -hl),
            new Vector3(-hw, h, hl),
            new Vector3(-hw, 0.04f, hl),
            new Vector3(-hw, 0.04f, -hl),
            -Vector3.UnitX
        );

        AddQuad(
            new Vector3(hw, h, hl),
            new Vector3(hw, h, -hl),
            new Vector3(hw, 0.04f, -hl),
            new Vector3(hw, 0.04f, hl),
            Vector3.UnitX
        );

        indexCount = indices.Count;

        vao = new VertexArrayObject();

        vertexVbo = new VertexBufferObject(vertices.ToArray().AsSpan());
        vao.LinkToVAO(0, 3, vertexVbo);

        uvVbo = new VertexBufferObject(uvs.ToArray().AsSpan());
        vao.LinkToVAO(1, 2, uvVbo);

        colorVbo = new VertexBufferObject(colors.ToArray().AsSpan());
        vao.LinkToVAO(2, 3, colorVbo);

        normalVbo = new VertexBufferObject(normals.ToArray().AsSpan());
        vao.LinkToVAO(3, 3, normalVbo);

        ibo = new IndexBufferObject(indices.ToArray().AsSpan());

        isInitialized = true;
    }

    public static void Render(IReadOnlyList<Minecart> minecarts, ShaderProgram shaderProgram)
    {
        if (minecarts == null || minecarts.Count == 0) return;

        var def = BlockRegistry.Get(BlockType.MinecartItem);
        var currentUV = def.GetFaceAtlasCoord(Face.Front);
        var currentTint = def.TintColor;

        if (!isInitialized || currentUV != cachedUV || currentTint != cachedTint)
        {
            Cleanup();
            Initialize();
        }

        if (vao == null || ibo == null || indexCount == 0) return;

        var modelLoc = GL.GetUniformLocation(shaderProgram.ID, "model");
        vao.Bind();
        ibo.Bind();

        lock (minecarts)
        {
            for (var i = 0; i < minecarts.Count; i++)
            {
                var cart = minecarts[i];
                if (cart == null || !cart.IsAlive) continue;

                var model = Matrix4.CreateRotationY(cart.RotationY) * Matrix4.CreateTranslation(cart.Position);
                GL.UniformMatrix4f(modelLoc, 1, true, ref model);
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
            }
        }

        // Restore identity model matrix
        var identity = Matrix4.Identity;
        GL.UniformMatrix4f(modelLoc, 1, true, ref identity);
    }

    public static void Cleanup()
    {
        if (isInitialized)
        {
            vao?.Delete();
            vertexVbo?.Delete();
            uvVbo?.Delete();
            colorVbo?.Delete();
            normalVbo?.Delete();
            ibo?.Delete();
            isInitialized = false;
        }
    }
}