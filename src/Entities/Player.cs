using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src.Audio;
using VelmoraCraft.src.Physics;
using VelmoraCraft.src.UI;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.Entities;

public class Player
{
    public readonly List<ItemDrop> DroppedItems = new();

    private readonly List<AABB> candidateBoxes = new(64);
    private float cactusDamageTimer;
    private float drownDamageTimer;

    private bool fKeyWasDown;
    private float footstepTimer;
    private float ladderSoundTimer;
    private float lavaDamageTimer;

    private bool rightClickWasDown;
    private float timeSinceLastDamage = 10.0f;
    private bool wasInLiquid;
    private float waterSoundTimer;

    public Player(Vector3 startPosition, Camera camera)
    {
        Position = startPosition;
        SpawnPosition = startPosition;
        Camera = camera;
        Health = MaxHealth;
        Oxygen = MaxOxygen;
        Stamina = MaxStamina;
        UpdateCamera();
    }

    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; } = Vector3.Zero;

    public float Width { get; } = 0.6f;
    public float Height { get; } = 1.8f;
    public float EyeHeight { get; } = 1.62f;

    public bool IsGrounded { get; set; }
    public bool IsFlying { get; set; }

    public float MoveSpeed { get; set; } = 4.3f;
    public float SprintMultiplier { get; set; } = 1.4f;
    public float JumpForce { get; set; } = 7.5f;
    public float Gravity { get; set; } = 22.0f;

    public Camera Camera { get; }

    public Vector3i? TargetedBlock { get; private set; }
    public Vector3i? PlacementBlock { get; private set; }
    public float BreakingProgress { get; private set; }
    public BlockType SelectedPlacementBlock => Inventory.Slots[SelectedHotbarSlot].Type;

    public bool IsRiding { get; private set; }
    public Minecart? CurrentMinecart { get; private set; }
    public Minecart? TargetedMinecart { get; private set; }

    public Inventory Inventory { get; } = new(36);
    public int SelectedHotbarSlot { get; set; }
    public ChestManager? ChestManager { get; set; }
    public Action<Vector3i, Inventory>? OnOpenChest { get; set; }
    public Action? OnOpenCraftingTable { get; set; }
    public Action<Vector3i>? OnOpenFurnace { get; set; }
    public AudioManager? AudioManager { get; set; }
    public Action? OnPortalEntered { get; set; }
    public Action<Vector3i>? OnUseBed { get; set; }
    public Action? OnFlyToggled { get; set; }

    public int BlocksBroken { get; set; }
    public int BlocksPlaced { get; set; }

    public float MaxHealth { get; set; } = 100.0f;
    public float Health { get; set; } = 100.0f;
    public float MaxOxygen { get; set; } = 100.0f;
    public float Oxygen { get; set; } = 100.0f;
    public float MaxStamina { get; set; } = 100.0f;
    public float Stamina { get; set; } = 100.0f;
    public bool IsSprinting { get; private set; }
    public bool IsUnderwater { get; private set; }
    public Vector3 SpawnPosition { get; set; }
    public float FallDistance { get; set; }

    public float DamageFlashTimer { get; private set; }

    public void MountMinecart(Minecart cart)
    {
        IsRiding = true;
        CurrentMinecart = cart;
        cart.Mount(this);
    }

    public void DismountMinecart()
    {
        if (CurrentMinecart != null)
        {
            Position = CurrentMinecart.Position + new Vector3(0, 0.85f, 0);
            CurrentMinecart.Dismount();
        }

        IsRiding = false;
        CurrentMinecart = null;
    }

    public AABB GetAABB()
    {
        var halfW = Width * 0.5f;
        return new AABB(
            Position.X - halfW, Position.Y, Position.Z - halfW,
            Position.X + halfW, Position.Y + Height, Position.Z + halfW
        );
    }

    public void ProcessMouseMovement(Vector2 mouseDelta)
    {
        Camera.Rotate(mouseDelta.X * 0.15f, -mouseDelta.Y * 0.15f);
    }

    public void Update(World.World world, KeyboardState keyboard, MouseState mouse, float deltaTime)
    {
        if (IsRiding && CurrentMinecart != null)
        {
            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
            {
                DismountMinecart();
            }
            else
            {
                Position = CurrentMinecart.Position + new Vector3(0, 0.35f, 0);
                Velocity = Vector3.Zero;
                IsGrounded = true;

                var fwd = new Vector3(Camera.Forward.X, 0, Camera.Forward.Z);
                if (fwd.LengthSquared > 0.001f) fwd = Vector3.Normalize(fwd);

                if (keyboard.IsKeyDown(Keys.W))
                    CurrentMinecart.ApplyPush(fwd * 14.0f * deltaTime);
                else if (keyboard.IsKeyDown(Keys.S)) CurrentMinecart.Velocity *= MathF.Pow(0.1f, deltaTime);

                IsSprinting = false;
                Camera.CurrentFov =
                    MathHelper.Lerp(Camera.CurrentFov, Camera.BaseFov, Math.Min(1.0f, deltaTime * 8.0f));
                UpdateCamera();
                UpdateRaycast(world);
                HandleBlockInteractions(world, keyboard, mouse, deltaTime);
                RedstoneSystem.Update(world, deltaTime);
                UpdateItemDrops(world, deltaTime);
                UpdateHealthAndOxygen(world, deltaTime);
                return;
            }
        }

        HandleFlyToggle(keyboard);

        if (IsFlying)
        {
            IsSprinting = false;
            UpdateFlyMovement(keyboard, deltaTime);
        }
        else
        {
            UpdateWalkMovement(world, keyboard, deltaTime);
        }

        var targetFov = IsSprinting ? Camera.BaseFov + 10.0f : Camera.BaseFov;
        Camera.CurrentFov = MathHelper.Lerp(Camera.CurrentFov, targetFov, Math.Min(1.0f, deltaTime * 8.0f));

        UpdateCamera();
        UpdateRaycast(world);
        HandleBlockInteractions(world, keyboard, mouse, deltaTime);
        RedstoneSystem.Update(world, deltaTime);
        UpdateItemDrops(world, deltaTime);
        UpdateHealthAndOxygen(world, deltaTime);

        if (PortalSystem.IsPlayerInsidePortal(world, this)) OnPortalEntered?.Invoke();
    }

    private void UpdateItemDrops(World.World world, float deltaTime)
    {
        for (var i = DroppedItems.Count - 1; i >= 0; i--)
        {
            var drop = DroppedItems[i];
            drop.Update(world, deltaTime);

            if (drop.IsAlive && Vector3.Distance(drop.Position, Position + new Vector3(0, 0.9f, 0)) < 1.8f)
                if (Inventory.AddItem(drop.ItemType, drop.Count))
                    drop.IsAlive = false;

            if (!drop.IsAlive) DroppedItems.RemoveAt(i);
        }
    }

    private void HandleFlyToggle(KeyboardState keyboard)
    {
        if (keyboard.IsKeyDown(Keys.F) && !fKeyWasDown)
        {
            IsFlying = !IsFlying;
            Velocity = Vector3.Zero;
            OnFlyToggled?.Invoke();
        }

        fKeyWasDown = keyboard.IsKeyDown(Keys.F);
    }

    private void UpdateFlyMovement(KeyboardState keyboard, float deltaTime)
    {
        var speed = MoveSpeed * 3.5f;
        if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl)) speed *= 2.0f;

        var move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += Camera.Forward;
        if (keyboard.IsKeyDown(Keys.S)) move -= Camera.Forward;
        if (keyboard.IsKeyDown(Keys.D)) move += Camera.Right;
        if (keyboard.IsKeyDown(Keys.A)) move -= Camera.Right;
        if (keyboard.IsKeyDown(Keys.Space)) move += Vector3.UnitY;
        if (keyboard.IsKeyDown(Keys.LeftShift)) move -= Vector3.UnitY;

        if (move.LengthSquared > 0.001f)
        {
            move = Vector3.Normalize(move);
            Position += move * speed * deltaTime;
        }

        ClampToWorldBounds();
        FallDistance = 0f;
    }

    private void UpdateWalkMovement(World.World world, KeyboardState keyboard, float deltaTime)
    {
        var footX = (int)Math.Floor(Position.X);
        var footY = (int)Math.Floor(Position.Y + 0.2f);
        var footZ = (int)Math.Floor(Position.Z);
        var bodyY = (int)Math.Floor(Position.Y + 0.9f);
        var footBlock = world.GetBlockWorld(footX, footY, footZ);
        var bodyBlock = world.GetBlockWorld(footX, bodyY, footZ);
        var inLiquid = footBlock == (byte)BlockType.Water || footBlock == (byte)BlockType.Lava ||
                       bodyBlock == (byte)BlockType.Water || bodyBlock == (byte)BlockType.Lava;
        var isWater = footBlock == (byte)BlockType.Water || bodyBlock == (byte)BlockType.Water;

        var eyeX = (int)Math.Floor(Position.X);
        var eyeY = (int)Math.Floor(Position.Y + EyeHeight);
        var eyeZ = (int)Math.Floor(Position.Z);
        var eyeBlock = world.GetBlockWorld(eyeX, eyeY, eyeZ);
        var isHeadInWater = eyeBlock == (byte)BlockType.Water;
        IsUnderwater = isHeadInWater || (isWater && Oxygen < MaxOxygen * 0.99f);

        if (inLiquid && !wasInLiquid && isWater) AudioManager?.PlayCategorySound("water", 0.85f, 0.15f);
        wasInLiquid = inLiquid;

        var forwardPlanar = Vector3.Normalize(new Vector3(Camera.Forward.X, 0, Camera.Forward.Z));
        var rightPlanar = Vector3.Normalize(new Vector3(Camera.Right.X, 0, Camera.Right.Z));

        var wishDir = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) wishDir += forwardPlanar;
        if (keyboard.IsKeyDown(Keys.S)) wishDir -= forwardPlanar;
        if (keyboard.IsKeyDown(Keys.D)) wishDir += rightPlanar;
        if (keyboard.IsKeyDown(Keys.A)) wishDir -= rightPlanar;

        if (wishDir.LengthSquared > 0.001f) wishDir = Vector3.Normalize(wishDir);

        var sprintKey = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
        var isMovingHorizontally = wishDir.LengthSquared > 0.001f;
        var speed = MoveSpeed;

        if (inLiquid)
        {
            IsSprinting = sprintKey && (isMovingHorizontally || keyboard.IsKeyDown(Keys.Space) ||
                                        keyboard.IsKeyDown(Keys.LeftShift));
            if (IsSprinting) speed *= SprintMultiplier;
            Stamina = MathF.Min(MaxStamina, Stamina + MaxStamina / 8.0f * deltaTime);
            speed *= 0.65f;
        }
        else
        {
            if (sprintKey && isMovingHorizontally && Stamina > 0.5f)
            {
                IsSprinting = true;
                speed *= SprintMultiplier;
                Stamina = MathF.Max(0f, Stamina - MaxStamina / 30.0f * deltaTime);
            }
            else
            {
                IsSprinting = false;
                Stamina = MathF.Min(MaxStamina, Stamina + MaxStamina / 8.0f * deltaTime);
            }
        }

        var targetVel = wishDir * speed;
        var horizontalLerp = inLiquid ? 6.0f : IsGrounded ? 12.0f : 2.5f;
        var vx = MathHelper.Lerp(Velocity.X, targetVel.X, horizontalLerp * deltaTime);
        var vz = MathHelper.Lerp(Velocity.Z, targetVel.Z, horizontalLerp * deltaTime);

        float vy;
        if (inLiquid)
        {
            vy = MathHelper.Lerp(Velocity.Y, -1.8f, 4.0f * deltaTime);
            if (keyboard.IsKeyDown(Keys.Space))
                vy = IsSprinting ? 5.0f : 3.8f;
            else if (keyboard.IsKeyDown(Keys.LeftShift)) vy = IsSprinting ? -5.0f : -3.8f;

            FallDistance = 0f;
            Velocity = new Vector3(vx, vy, vz);

            if (isWater)
            {
                var isMoving = wishDir.LengthSquared > 0.001f || Math.Abs(Velocity.Y) > 0.6f;
                if (isMoving)
                {
                    waterSoundTimer += deltaTime;
                    if (waterSoundTimer > 0.42f)
                    {
                        waterSoundTimer = 0f;
                        AudioManager?.PlayCategorySound("water", 0.70f, 0.15f);
                    }
                }
                else
                {
                    waterSoundTimer = 0.35f;
                }
            }

            MoveWithCollision(world, Velocity * deltaTime);
            ClampToWorldBounds();
            return;
        }

        var onLadder = false;
        var ladderMinX = (int)Math.Floor(Position.X - Width * 0.5f);
        var ladderMaxX = (int)Math.Floor(Position.X + Width * 0.5f);
        var ladderMinY = (int)Math.Floor(Position.Y);
        var ladderMaxY = (int)Math.Floor(Position.Y + Height);
        var ladderMinZ = (int)Math.Floor(Position.Z - Width * 0.5f);
        var ladderMaxZ = (int)Math.Floor(Position.Z + Width * 0.5f);

        for (var lx = ladderMinX; lx <= ladderMaxX && !onLadder; lx++)
        for (var ly = ladderMinY; ly <= ladderMaxY && !onLadder; ly++)
        for (var lz = ladderMinZ; lz <= ladderMaxZ && !onLadder; lz++)
            if (world.GetBlockWorld(lx, ly, lz) == (byte)BlockType.Ladder)
                onLadder = true;

        if (onLadder)
        {
            float ladderVy;
            if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Space))
                ladderVy = 3.2f;
            else if (keyboard.IsKeyDown(Keys.S))
                ladderVy = -2.8f;
            else if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                ladderVy = 0f;
            else
                ladderVy = -1.0f;

            FallDistance = 0f;
            Velocity = new Vector3(vx * 0.6f, ladderVy, vz * 0.6f);

            if (Math.Abs(ladderVy) > 0.4f || vx * vx + vz * vz > 0.1f)
            {
                ladderSoundTimer += deltaTime;
                if (ladderSoundTimer > 0.35f)
                {
                    ladderSoundTimer = 0f;
                    AudioManager?.PlayCategorySound("wood", 0.5f, 0.1f);
                }
            }
            else
            {
                ladderSoundTimer = 0.25f;
            }

            MoveWithCollision(world, Velocity * deltaTime);
            ClampToWorldBounds();
            return;
        }

        var wasGrounded = IsGrounded;
        var startY = Position.Y;

        vy = Velocity.Y - Gravity * deltaTime;

        if (IsGrounded && keyboard.IsKeyDown(Keys.Space))
        {
            vy = JumpForce;
            IsGrounded = false;
            AudioManager?.PlayPlayerJump();
        }

        Velocity = new Vector3(vx, vy, vz);

        MoveWithCollision(world, Velocity * deltaTime);
        ClampToWorldBounds();

        var endY = Position.Y;
        var dropThisFrame = startY - endY;

        if (dropThisFrame > 0f)
            FallDistance += dropThisFrame;
        else if (Velocity.Y > 0f) FallDistance = 0f;

        if (!wasGrounded && IsGrounded)
        {
            AudioManager?.PlayPlayerLand();

            if (FallDistance > 3.0f)
            {
                var excessBlocks = FallDistance - 3.0f;
                var damage = excessBlocks * 10.0f;
                TakeDamage(damage, "fall", world);
            }

            FallDistance = 0f;
        }
        else if (IsGrounded)
        {
            FallDistance = 0f;
        }

        if (IsGrounded && vx * vx + vz * vz > 0.4f)
        {
            footstepTimer += deltaTime;
            if (footstepTimer > 0.38f)
            {
                footstepTimer = 0f;
                var groundX = (int)Math.Floor(Position.X);
                var groundY = (int)Math.Floor(Position.Y - 0.2f);
                var groundZ = (int)Math.Floor(Position.Z);
                var groundBlock = world.GetBlockWorld(groundX, groundY, groundZ);

                if (footBlock == (byte)BlockType.Water)
                    AudioManager?.PlayCategorySound("water", 0.60f, 0.15f);
                else
                    AudioManager?.PlayBlockStepSound((BlockType)groundBlock);
            }
        }
        else
        {
            footstepTimer = 0.25f;
        }
    }

    public void ResolveStuckInBlocks(World.World world)
    {
        if (IsFlying) return;

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var pBox = GetAABB();
            var minX = (int)MathF.Floor(pBox.Min.X);
            var maxX = (int)MathF.Ceiling(pBox.Max.X);
            var minY = (int)MathF.Floor(pBox.Min.Y);
            var maxY = (int)MathF.Ceiling(pBox.Max.Y);
            var minZ = (int)MathF.Floor(pBox.Min.Z);
            var maxZ = (int)MathF.Ceiling(pBox.Max.Z);

            var pushUpTarget = float.MinValue;
            var isIntersecting = false;

            for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
            for (var z = minZ; z <= maxZ; z++)
            {
                var b = world.GetBlockWorld(x, y, z);
                if (BlockRegistry.IsSolid(b))
                {
                    var blockBox = new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f);
                    if (pBox.Intersects(blockBox))
                    {
                        isIntersecting = true;
                        if (blockBox.Max.Y > pBox.Min.Y && blockBox.Max.Y <= pBox.Max.Y + 0.1f)
                            if (blockBox.Max.Y > pushUpTarget)
                                pushUpTarget = blockBox.Max.Y;
                    }
                }
            }

            if (!isIntersecting || pushUpTarget <= float.MinValue) break;

            Position = new Vector3(Position.X, pushUpTarget, Position.Z);
            Velocity = new Vector3(Velocity.X, MathF.Max(0, Velocity.Y), Velocity.Z);
            IsGrounded = true;
        }

        UpdateCamera();
    }

    private void MoveWithCollision(World.World world, Vector3 movement)
    {
        if (!IsFlying) ResolveStuckInBlocks(world);

        var playerBox = GetAABB();

        var minX = (int)MathF.Floor(playerBox.Min.X + MathF.Min(0, movement.X) - 0.5f);
        var maxX = (int)MathF.Ceiling(playerBox.Max.X + MathF.Max(0, movement.X) + 0.5f);
        var minY = (int)MathF.Floor(playerBox.Min.Y + MathF.Min(0, movement.Y) - 0.5f);
        var maxY = (int)MathF.Ceiling(playerBox.Max.Y + MathF.Max(0, movement.Y) + 0.5f);
        var minZ = (int)MathF.Floor(playerBox.Min.Z + MathF.Min(0, movement.Z) - 0.5f);
        var maxZ = (int)MathF.Ceiling(playerBox.Max.Z + MathF.Max(0, movement.Z) + 0.5f);

        candidateBoxes.Clear();
        for (var x = minX; x <= maxX; x++)
        for (var y = minY; y <= maxY; y++)
        for (var z = minZ; z <= maxZ; z++)
        {
            var b = world.GetBlockWorld(x, y, z);
            if (BlockRegistry.IsSolid(b))
                candidateBoxes.Add(new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f));
        }

        var hitGroundThisFrame = false;

        if (movement.Y != 0)
        {
            foreach (var box in candidateBoxes)
                if (playerBox.Min.X < box.Max.X && playerBox.Max.X > box.Min.X &&
                    playerBox.Min.Z < box.Max.Z && playerBox.Max.Z > box.Min.Z)
                {
                    if (movement.Y > 0 && playerBox.Max.Y <= box.Min.Y && playerBox.Max.Y + movement.Y > box.Min.Y)
                    {
                        movement.Y = box.Min.Y - playerBox.Max.Y;
                        Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
                    }
                    else if (movement.Y < 0 && playerBox.Min.Y >= box.Max.Y - 0.5f &&
                             playerBox.Min.Y + movement.Y <= box.Max.Y && playerBox.Max.Y > box.Max.Y)
                    {
                        movement.Y = box.Max.Y - playerBox.Min.Y;
                        Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
                        hitGroundThisFrame = true;
                    }
                }

            Position = new Vector3(Position.X, Position.Y + movement.Y, Position.Z);
            playerBox = GetAABB();
        }

        if (hitGroundThisFrame)
            IsGrounded = true;
        else if (movement.Y < -0.001f || movement.Y > 0.001f) IsGrounded = false;

        if (movement.X != 0)
        {
            foreach (var box in candidateBoxes)
                if (playerBox.Min.Y < box.Max.Y && playerBox.Max.Y > box.Min.Y &&
                    playerBox.Min.Z < box.Max.Z && playerBox.Max.Z > box.Min.Z)
                {
                    if (movement.X > 0 && playerBox.Max.X <= box.Min.X && playerBox.Max.X + movement.X > box.Min.X)
                    {
                        movement.X = box.Min.X - playerBox.Max.X;
                        Velocity = new Vector3(0, Velocity.Y, Velocity.Z);
                    }
                    else if (movement.X < 0 && playerBox.Min.X >= box.Max.X && playerBox.Min.X + movement.X < box.Max.X)
                    {
                        movement.X = box.Max.X - playerBox.Min.X;
                        Velocity = new Vector3(0, Velocity.Y, Velocity.Z);
                    }
                }

            Position = new Vector3(Position.X + movement.X, Position.Y, Position.Z);
            playerBox = GetAABB();
        }

        if (movement.Z != 0)
        {
            foreach (var box in candidateBoxes)
                if (playerBox.Min.X < box.Max.X && playerBox.Max.X > box.Min.X &&
                    playerBox.Min.Y < box.Max.Y && playerBox.Max.Y > box.Min.Y)
                {
                    if (movement.Z > 0 && playerBox.Max.Z <= box.Min.Z && playerBox.Max.Z + movement.Z > box.Min.Z)
                    {
                        movement.Z = box.Min.Z - playerBox.Max.Z;
                        Velocity = new Vector3(Velocity.X, Velocity.Y, 0);
                    }
                    else if (movement.Z < 0 && playerBox.Min.Z >= box.Max.Z && playerBox.Min.Z + movement.Z < box.Max.Z)
                    {
                        movement.Z = box.Max.Z - playerBox.Min.Z;
                        Velocity = new Vector3(Velocity.X, Velocity.Y, 0);
                    }
                }

            Position = new Vector3(Position.X, Position.Y, Position.Z + movement.Z);
        }
    }

    private void ClampToWorldBounds()
    {
        var pos = Position;
        pos.X = Math.Clamp(pos.X, 1.0f, World.World.WORLD_SIZE_X - 1.0f);
        pos.Z = Math.Clamp(pos.Z, 1.0f, World.World.WORLD_SIZE_Z - 1.0f);
        pos.Y = Math.Clamp(pos.Y, 1.0f, 125.0f);
        Position = pos;
    }

    private void UpdateCamera()
    {
        Camera.Position = Position + new Vector3(0, EyeHeight, 0);
    }

    private void UpdateRaycast(World.World world)
    {
        var hit = VoxelRaycaster.Raycast(world, Camera.Position, Camera.Forward, 5.5f);
        if (hit.HasHit)
        {
            TargetedBlock = hit.BlockPos;
            PlacementBlock = hit.BlockPos + hit.Normal;
        }
        else
        {
            TargetedBlock = null;
            PlacementBlock = null;
            BreakingProgress = 0.0f;
        }

        TargetedMinecart = null;
        var closestDist = hit.HasHit ? hit.Distance : 5.5f;

        lock (world.Minecarts)
        {
            for (var i = 0; i < world.Minecarts.Count; i++)
            {
                var cart = world.Minecarts[i];
                if (cart == null || !cart.IsAlive || cart == CurrentMinecart) continue;

                if (cart.GetAABB().IntersectsRay(Camera.Position, Camera.Forward, 5.5f, out var cartDist))
                    if (cartDist < closestDist)
                    {
                        closestDist = cartDist;
                        TargetedMinecart = cart;
                    }
            }
        }
    }

    private void HandleBlockInteractions(World.World world, KeyboardState keyboard, MouseState mouse, float deltaTime)
    {
        if (TargetedMinecart != null)
        {
            if (mouse.IsButtonDown(MouseButton.Left) && !IsRiding)
            {
                TargetedMinecart.IsAlive = false;
                lock (world.Minecarts)
                {
                    world.Minecarts.Remove(TargetedMinecart);
                }

                DroppedItems.Add(new ItemDrop(BlockType.MinecartItem,
                    TargetedMinecart.Position + new Vector3(0, 0.2f, 0)));
                TargetedMinecart = null;
                return;
            }

            if (mouse.IsButtonDown(MouseButton.Right) && !rightClickWasDown)
                if (!IsRiding)
                {
                    MountMinecart(TargetedMinecart);
                    rightClickWasDown = true;
                    return;
                }
        }

        if (IsRiding && mouse.IsButtonDown(MouseButton.Right) && !rightClickWasDown)
        {
            DismountMinecart();
            rightClickWasDown = true;
            return;
        }

        if (mouse.IsButtonDown(MouseButton.Left) && TargetedBlock.HasValue)
        {
            var bp = TargetedBlock.Value;
            var blockId = world.GetBlockWorld(bp.X, bp.Y, bp.Z);
            var def = BlockRegistry.Get(blockId);

            if (def.Hardness >= 0)
            {
                var breakTime = MathF.Max(0.15f, def.Hardness * 0.8f);
                BreakingProgress += deltaTime / breakTime;

                if (BreakingProgress >= 1.0f)
                {
                    world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Empty);
                    world.FluidSystem.OnBlockBroken(world, bp);
                    BlocksBroken++;
                    AudioManager?.PlayBlockBreakSound(def.Type);

                    var dropType = def.Type switch
                    {
                        BlockType.Stone => BlockType.Cobblestone,
                        BlockType.CoalOre => BlockType.Coal,
                        _ => def.Type
                    };

                    if (def.Type is BlockType.DoorLower or BlockType.DoorLowerOpen)
                    {
                        dropType = BlockType.DoorLower;
                        var up = bp + Vector3i.UnitY;
                        var upId = world.GetBlockWorld(up.X, up.Y, up.Z);
                        if (upId is (byte)BlockType.DoorUpper or (byte)BlockType.DoorUpperOpen)
                            world.SetBlockWorld(up.X, up.Y, up.Z, BlockType.Empty);
                    }
                    else if (def.Type is BlockType.DoorUpper or BlockType.DoorUpperOpen)
                    {
                        dropType = BlockType.DoorLower;
                        var down = bp - Vector3i.UnitY;
                        var downId = world.GetBlockWorld(down.X, down.Y, down.Z);
                        if (downId is (byte)BlockType.DoorLower or (byte)BlockType.DoorLowerOpen)
                            world.SetBlockWorld(down.X, down.Y, down.Z, BlockType.Empty);
                    }
                    else if (def.Type is BlockType.IronDoorLower or BlockType.IronDoorLowerOpen)
                    {
                        dropType = BlockType.IronDoorLower;
                        var up = bp + Vector3i.UnitY;
                        var upId = world.GetBlockWorld(up.X, up.Y, up.Z);
                        if (upId is (byte)BlockType.IronDoorUpper or (byte)BlockType.IronDoorUpperOpen)
                            world.SetBlockWorld(up.X, up.Y, up.Z, BlockType.Empty);
                    }
                    else if (def.Type is BlockType.IronDoorUpper or BlockType.IronDoorUpperOpen)
                    {
                        dropType = BlockType.IronDoorLower;
                        var down = bp - Vector3i.UnitY;
                        var downId = world.GetBlockWorld(down.X, down.Y, down.Z);
                        if (downId is (byte)BlockType.IronDoorLower or (byte)BlockType.IronDoorLowerOpen)
                            world.SetBlockWorld(down.X, down.Y, down.Z, BlockType.Empty);
                    }
                    else if (def.Type == BlockType.LeverOn)
                    {
                        dropType = BlockType.Lever;
                        RedstoneSystem.PropagatePower(world, bp, false);
                    }
                    else if (def.Type == BlockType.StoneButtonPressed)
                    {
                        dropType = BlockType.StoneButton;
                        RedstoneSystem.PropagatePower(world, bp, false);
                    }
                    else if (def.Type == BlockType.PoweredRailOn)
                    {
                        dropType = BlockType.PoweredRail;
                    }
                    else if (def.Type is BlockType.BedFoot or BlockType.BedHead)
                    {
                        dropType = BlockType.BedFoot;
                        ReadOnlySpan<Vector3i> bedOffsets = stackalloc Vector3i[]
                        {
                            Vector3i.UnitX, -Vector3i.UnitX, Vector3i.UnitZ, -Vector3i.UnitZ
                        };
                        foreach (var off in bedOffsets)
                        {
                            var nPos = bp + off;
                            var nId = world.GetBlockWorld(nPos.X, nPos.Y, nPos.Z);
                            if (nId is (byte)BlockType.BedFoot or (byte)BlockType.BedHead)
                            {
                                world.SetBlockWorld(nPos.X, nPos.Y, nPos.Z, BlockType.Empty);
                                break;
                            }
                        }
                    }

                    if (def.Type is BlockType.Log or BlockType.BirchLog or BlockType.SpruceLog)
                    {
                        world.TriggerTreeLeafDecay(bp);
                        DroppedItems.Add(new ItemDrop(dropType, new Vector3(bp.X, bp.Y, bp.Z)));
                    }
                    else if (def.Type == BlockType.OakSapling)
                    {
                        world.GrowingSaplings.TryRemove(bp, out _);
                        DroppedItems.Add(new ItemDrop(BlockType.OakSapling, new Vector3(bp.X, bp.Y, bp.Z)));
                    }
                    else if (def.Type is BlockType.Leaves or BlockType.SpruceLeaves)
                    {
                        world.PendingDecayingLeaves.TryRemove(bp, out _);
                        if (Random.Shared.Next(100) < 20)
                            DroppedItems.Add(new ItemDrop(BlockType.OakSapling, new Vector3(bp.X, bp.Y, bp.Z)));
                    }
                    else if (def.Type == BlockType.TallGrass)
                    {
                        var roll = Random.Shared.Next(100);
                        if (roll < 60)
                            DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z)));
                        if (roll < 20) DroppedItems.Add(new ItemDrop(BlockType.Cobweb, new Vector3(bp.X, bp.Y, bp.Z)));
                    }
                    else if (def.Type == BlockType.Wheat)
                    {
                        var stage = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                        world.GrowingCrops.Remove(bp);
                        world.CropStageTimers.TryRemove(bp, out _);
                        if (stage >= 7)
                        {
                            DroppedItems.Add(new ItemDrop(BlockType.Wheat, new Vector3(bp.X, bp.Y, bp.Z)));
                            var seeds = 1 + Random.Shared.Next(2);
                            DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z), seeds));
                        }
                        else
                        {
                            DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z)));
                        }
                    }
                    else
                    {
                        DroppedItems.Add(new ItemDrop(dropType, new Vector3(bp.X, bp.Y, bp.Z)));
                    }

                    if (def.Type == BlockType.Chest && ChestManager != null)
                    {
                        var chestInv = ChestManager.GetOrCreateChest(bp);
                        for (var ci = 0; ci < chestInv.SlotCount; ci++)
                            if (!chestInv.Slots[ci].IsEmpty)
                            {
                                DroppedItems.Add(new ItemDrop(chestInv.Slots[ci].Type, new Vector3(bp.X, bp.Y, bp.Z),
                                    chestInv.Slots[ci].Count));
                                chestInv.Slots[ci].Clear();
                            }

                        ChestManager.RemoveChest(bp);
                    }

                    BreakingProgress = 0.0f;
                }
            }
        }
        else
        {
            BreakingProgress = 0.0f;
        }

        var rightMouseDown = mouse.IsButtonDown(MouseButton.Right);
        if (rightMouseDown && !rightClickWasDown)
        {
            var hotbarStack = Inventory.Slots[SelectedHotbarSlot];

            if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.FlintAndSteel)
            {
                var ignited = false;
                if (TargetedBlock.HasValue && PortalSystem.TryIgnitePortal(world, TargetedBlock.Value))
                    ignited = true;
                else if (PlacementBlock.HasValue && PortalSystem.TryIgnitePortal(world, PlacementBlock.Value))
                    ignited = true;

                if (ignited)
                {
                    AudioManager?.PlayPortalIgnite();
                    rightClickWasDown = true;
                    return;
                }
            }

            if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.MinecartItem && TargetedBlock.HasValue)
            {
                var bp = TargetedBlock.Value;
                var b = world.GetBlockWorld(bp.X, bp.Y, bp.Z);
                if (b is (byte)BlockType.Rail or (byte)BlockType.PoweredRail or (byte)BlockType.PoweredRailOn)
                {
                    var cart = new Minecart(new Vector3(bp.X, bp.Y + 0.1f, bp.Z));
                    lock (world.Minecarts)
                    {
                        world.Minecarts.Add(cart);
                    }

                    Inventory.Slots[SelectedHotbarSlot].Count--;
                    if (Inventory.Slots[SelectedHotbarSlot].Count <= 0) Inventory.Slots[SelectedHotbarSlot].Clear();
                    rightClickWasDown = true;
                    return;
                }
            }

            if (TargetedBlock.HasValue)
            {
                var bp = TargetedBlock.Value;
                var blockId = world.GetBlockWorld(bp.X, bp.Y, bp.Z);
                var isShiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

                if (isShiftDown)
                {
                    if (blockId == (byte)BlockType.Chest)
                    {
                        if (ChestManager.TryGetDoubleChestPartner(bp, (x, y, z) => world.GetBlockWorld(x, y, z),
                                out var partnerPos, out _, out var isAxisX))
                        {
                            var currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            var nextMeta = isAxisX
                                ? currentMeta == (byte)BlockFacing.South
                                    ? (byte)BlockFacing.North
                                    : (byte)BlockFacing.South
                                : currentMeta == (byte)BlockFacing.West
                                    ? (byte)BlockFacing.East
                                    : (byte)BlockFacing.West;
                            world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                            world.SetBlockMetadata(partnerPos.X, partnerPos.Y, partnerPos.Z, nextMeta);
                            AudioManager?.PlayCategorySound("wood", 0.5f, 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                        else
                        {
                            var currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            var nextMeta = (byte)((currentMeta + 1) % 4);
                            world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                            AudioManager?.PlayCategorySound("wood", 0.5f, 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                    }

                    if (BlockRegistry.IsDoorBlock(blockId))
                    {
                        var otherPos = blockId is (byte)BlockType.DoorLower or (byte)BlockType.DoorLowerOpen
                            or (byte)BlockType.IronDoorLower or (byte)BlockType.IronDoorLowerOpen
                            ? bp + Vector3i.UnitY
                            : bp - Vector3i.UnitY;

                        var currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                        var nextMeta = (byte)((currentMeta + 1) % 4);
                        world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                        if (BlockRegistry.IsDoorBlock(world.GetBlockWorld(otherPos.X, otherPos.Y, otherPos.Z)))
                            world.SetBlockMetadata(otherPos.X, otherPos.Y, otherPos.Z, nextMeta);
                        AudioManager?.PlayCategorySound("wood", 0.5f, 0.1f);
                        rightClickWasDown = true;
                        return;
                    }

                    if (BlockRegistry.IsDirectionalBlock((BlockType)blockId))
                    {
                        var currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                        var nextMeta = (byte)((currentMeta + 1) % 4);
                        world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                        AudioManager?.PlayCategorySound("wood", 0.5f, 0.1f);
                        rightClickWasDown = true;
                        return;
                    }

                    if (BlockRegistry.IsRailBlock(blockId))
                    {
                        var currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                        var maxShapes = blockId == (byte)BlockType.Rail ? 6 : 2;
                        var nextMeta = (byte)((currentMeta + 1) % maxShapes);
                        world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                        AudioManager?.PlayCategorySound("stone", 0.5f, 0.1f);
                        rightClickWasDown = true;
                        return;
                    }

                    if (blockId is (byte)BlockType.Grass or (byte)BlockType.Dirt)
                    {
                        var hasWater = false;
                        for (var dx = -2; dx <= 2 && !hasWater; dx++)
                        for (var dz = -2; dz <= 2 && !hasWater; dz++)
                        for (var dy = -1; dy <= 1; dy++)
                            if (world.GetBlockWorld(bp.X + dx, bp.Y + dy, bp.Z + dz) == (byte)BlockType.Water)
                            {
                                hasWater = true;
                                break;
                            }

                        if (hasWater)
                        {
                            world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Farmland);
                            AudioManager?.PlayCategorySound("gravel", 0.8f, 0.15f);
                            rightClickWasDown = true;
                            return;
                        }
                    }
                }

                if (blockId == (byte)BlockType.Chest)
                    if (ChestManager != null && OnOpenChest != null)
                    {
                        var chestInv = ChestManager.GetChestInventory(bp, world);
                        OnOpenChest.Invoke(bp, chestInv);
                        AudioManager?.PlayCategorySound("wood", 0.8f, 0.1f);
                        rightClickWasDown = true;
                        return;
                    }

                if (blockId == (byte)BlockType.CraftingTable)
                    if (OnOpenCraftingTable != null)
                    {
                        OnOpenCraftingTable.Invoke();
                        AudioManager?.PlayCategorySound("wood", 0.7f);
                        rightClickWasDown = true;
                        return;
                    }

                if (blockId == (byte)BlockType.Furnace)
                    if (OnOpenFurnace != null)
                    {
                        OnOpenFurnace.Invoke(bp);
                        AudioManager?.PlayCategorySound("concrete", 0.7f);
                        rightClickWasDown = true;
                        return;
                    }

                if (blockId is (byte)BlockType.DoorLower or (byte)BlockType.DoorUpper
                    or (byte)BlockType.DoorLowerOpen or (byte)BlockType.DoorUpperOpen)
                {
                    RedstoneSystem.ToggleDoor(world, bp);
                    AudioManager?.PlayBlockPlaceSound(BlockType.Planks);
                    rightClickWasDown = true;
                    return;
                }

                if (blockId is (byte)BlockType.IronDoorLower or (byte)BlockType.IronDoorUpper
                    or (byte)BlockType.IronDoorLowerOpen or (byte)BlockType.IronDoorUpperOpen)
                {
                    RedstoneSystem.ToggleDoor(world, bp);
                    AudioManager?.PlayBlockPlaceSound(BlockType.IronBlock);
                    rightClickWasDown = true;
                    return;
                }

                if (blockId is (byte)BlockType.Lever or (byte)BlockType.LeverOn)
                {
                    RedstoneSystem.ToggleLever(world, bp);
                    AudioManager?.PlayBlockPlaceSound(BlockType.Cobblestone);
                    rightClickWasDown = true;
                    return;
                }

                if (blockId == (byte)BlockType.StoneButton)
                {
                    RedstoneSystem.PressButton(world, bp);
                    AudioManager?.PlayBlockPlaceSound(BlockType.Stone);
                    rightClickWasDown = true;
                    return;
                }

                if (blockId is (byte)BlockType.BedFoot or (byte)BlockType.BedHead)
                {
                    OnUseBed?.Invoke(bp);
                    rightClickWasDown = true;
                    return;
                }

                if (blockId == (byte)BlockType.Farmland && !hotbarStack.IsEmpty &&
                    hotbarStack.Type == BlockType.WheatSeeds)
                {
                    var plantPos = bp + Vector3i.UnitY;
                    if (plantPos.Y < Chunk.SIZE_Y && world.GetBlockWorld(plantPos.X, plantPos.Y, plantPos.Z) ==
                        (byte)BlockType.Empty)
                    {
                        world.SetBlockWorld(plantPos.X, plantPos.Y, plantPos.Z, BlockType.Wheat);
                        world.SetBlockMetadata(plantPos.X, plantPos.Y, plantPos.Z, 0);
                        world.GrowingCrops.Add(plantPos);
                        world.CropStageTimers[plantPos] = 25.0f + Random.Shared.NextSingle() * 10.0f;
                        AudioManager?.PlayBlockPlaceSound(BlockType.Grass);
                        Inventory.Slots[SelectedHotbarSlot].Count--;
                        if (Inventory.Slots[SelectedHotbarSlot].Count <= 0) Inventory.Slots[SelectedHotbarSlot].Clear();
                        rightClickWasDown = true;
                        return;
                    }
                }

                if (blockId == (byte)BlockType.Cake)
                    if (Health < MaxHealth || Stamina < MaxStamina)
                    {
                        Health = MathF.Min(MaxHealth, Health + 25.0f);
                        Stamina = MathF.Min(MaxStamina, Stamina + 30.0f);
                        world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Empty);
                        AudioManager?.PlayCategorySound("cloth", 0.9f);
                        rightClickWasDown = true;
                        return;
                    }
            }

            if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.Bread)
                if (Health < MaxHealth || Stamina < MaxStamina)
                {
                    Health = MathF.Min(MaxHealth, Health + 25.0f);
                    Stamina = MathF.Min(MaxStamina, Stamina + 25.0f);
                    Inventory.Slots[SelectedHotbarSlot].Count--;
                    if (Inventory.Slots[SelectedHotbarSlot].Count <= 0) Inventory.Slots[SelectedHotbarSlot].Clear();
                    AudioManager?.PlayCategorySound("wood", 0.8f);
                    rightClickWasDown = true;
                    return;
                }

            if (PlacementBlock.HasValue)
            {
                var pb = PlacementBlock.Value;
                hotbarStack = Inventory.Slots[SelectedHotbarSlot];

                if (!hotbarStack.IsEmpty && hotbarStack.Type != BlockType.Empty)
                {
                    var placeDef = BlockRegistry.Get(hotbarStack.Type);

                    if (hotbarStack.Type is BlockType.DoorLower or BlockType.IronDoorLower)
                    {
                        if (pb.Y + 1 < Chunk.SIZE_Y &&
                            world.GetBlockWorld(pb.X, pb.Y + 1, pb.Z) == (byte)BlockType.Empty)
                        {
                            var lowerBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.5f,
                                pb.Z + 0.5f);
                            var upperBox = new AABB(pb.X - 0.5f, pb.Y + 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 1.5f,
                                pb.Z + 0.5f);
                            var pBox = GetAABB();

                            if (!pBox.Intersects(lowerBox) && !pBox.Intersects(upperBox))
                            {
                                var isIron = hotbarStack.Type == BlockType.IronDoorLower;
                                byte facing;
                                if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                    facing = Camera.Forward.Z < 0 ? (byte)BlockFacing.South : (byte)BlockFacing.North;
                                else
                                    facing = Camera.Forward.X < 0 ? (byte)BlockFacing.East : (byte)BlockFacing.West;

                                world.SetBlockWorld(pb.X, pb.Y, pb.Z,
                                    isIron ? BlockType.IronDoorLower : BlockType.DoorLower, facing);
                                world.SetBlockWorld(pb.X, pb.Y + 1, pb.Z,
                                    isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper, facing);
                                BlocksPlaced += 2;
                                AudioManager?.PlayBlockPlaceSound(isIron ? BlockType.IronBlock : BlockType.Planks);
                                Inventory.Slots[SelectedHotbarSlot].Count--;
                                if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                                    Inventory.Slots[SelectedHotbarSlot].Clear();
                            }
                        }
                    }
                    else if (hotbarStack.Type == BlockType.BedFoot)
                    {
                        Vector3i forwardOffset;
                        byte facing;

                        if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                        {
                            forwardOffset = Camera.Forward.Z > 0 ? Vector3i.UnitZ : -Vector3i.UnitZ;
                            facing = Camera.Forward.Z > 0 ? (byte)0 : (byte)1;
                        }
                        else
                        {
                            forwardOffset = Camera.Forward.X > 0 ? Vector3i.UnitX : -Vector3i.UnitX;
                            facing = Camera.Forward.X > 0 ? (byte)3 : (byte)2;
                        }

                        var headPos = pb + forwardOffset;
                        if (world.GetBlockWorld(headPos.X, headPos.Y, headPos.Z) == (byte)BlockType.Empty)
                        {
                            var footBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.1f,
                                pb.Z + 0.5f);
                            var headBox = new AABB(headPos.X - 0.5f, headPos.Y - 0.5f, headPos.Z - 0.5f,
                                headPos.X + 0.5f, headPos.Y + 0.1f, headPos.Z + 0.5f);
                            var pBox = GetAABB();

                            if (!pBox.Intersects(footBox) && !pBox.Intersects(headBox))
                            {
                                world.SetBlockWorld(pb.X, pb.Y, pb.Z, BlockType.BedFoot);
                                world.SetBlockMetadata(pb.X, pb.Y, pb.Z, facing);

                                world.SetBlockWorld(headPos.X, headPos.Y, headPos.Z, BlockType.BedHead);
                                world.SetBlockMetadata(headPos.X, headPos.Y, headPos.Z, facing);

                                BlocksPlaced += 2;
                                AudioManager?.PlayBlockPlaceSound(BlockType.Planks);
                                Inventory.Slots[SelectedHotbarSlot].Count--;
                                if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                                    Inventory.Slots[SelectedHotbarSlot].Clear();
                            }
                        }
                    }
                    else
                    {
                        var canPlace = true;
                        if (placeDef.IsSolid)
                        {
                            var blockBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.5f,
                                pb.Z + 0.5f);
                            if (GetAABB().Intersects(blockBox)) canPlace = false;
                        }

                        if (canPlace)
                        {
                            var toPlace = hotbarStack.Type;

                            if (toPlace == BlockType.PoweredRail && RedstoneSystem.HasActivePowerNeighbor(world, pb))
                                toPlace = BlockType.PoweredRailOn;

                            if (toPlace == BlockType.WheatSeeds)
                            {
                                var under = world.GetBlockWorld(pb.X, pb.Y - 1, pb.Z);
                                if (under != (byte)BlockType.Farmland)
                                {
                                    canPlace = false;
                                }
                                else
                                {
                                    toPlace = BlockType.Wheat;
                                    world.GrowingCrops.Add(pb);
                                    world.CropStageTimers[pb] = 25.0f + Random.Shared.NextSingle() * 10.0f;
                                }
                            }
                            else if (toPlace == BlockType.OakSapling)
                            {
                                var under = world.GetBlockWorld(pb.X, pb.Y - 1, pb.Z);
                                if (under != (byte)BlockType.Grass && under != (byte)BlockType.Dirt &&
                                    under != (byte)BlockType.SnowyGrass)
                                    canPlace = false;
                                else
                                    world.RegisterGrowingSapling(pb);
                            }

                            if (!canPlace) return;

                            byte metadata = 0;
                            if (toPlace == BlockType.Chest)
                            {
                                ReadOnlySpan<Vector3i> chestNeighbors = stackalloc Vector3i[]
                                {
                                    new Vector3i(pb.X + 1, pb.Y, pb.Z),
                                    new Vector3i(pb.X - 1, pb.Y, pb.Z),
                                    new Vector3i(pb.X, pb.Y, pb.Z + 1),
                                    new Vector3i(pb.X, pb.Y, pb.Z - 1)
                                };

                                var foundFirstChest = false;
                                foreach (var n in chestNeighbors)
                                    if (world.GetBlockWorld(n.X, n.Y, n.Z) == (byte)BlockType.Chest)
                                    {
                                        var firstFacing = world.GetBlockMetadata(n.X, n.Y, n.Z);
                                        var isAxisX = pb.X != n.X;

                                        metadata = isAxisX
                                            ? firstFacing == (byte)BlockFacing.North
                                                ? (byte)BlockFacing.North
                                                : (byte)BlockFacing.South
                                            : firstFacing == (byte)BlockFacing.East
                                                ? (byte)BlockFacing.East
                                                : (byte)BlockFacing.West;

                                        world.SetBlockMetadata(n.X, n.Y, n.Z, metadata);
                                        foundFirstChest = true;
                                        break;
                                    }

                                if (!foundFirstChest)
                                {
                                    if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                        metadata = Camera.Forward.Z < 0
                                            ? (byte)BlockFacing.South
                                            : (byte)BlockFacing.North;
                                    else
                                        metadata = Camera.Forward.X < 0
                                            ? (byte)BlockFacing.East
                                            : (byte)BlockFacing.West;
                                }
                            }
                            else if (BlockRegistry.IsDirectionalBlock(toPlace))
                            {
                                if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                    metadata = Camera.Forward.Z < 0 ? (byte)BlockFacing.South : (byte)BlockFacing.North;
                                else
                                    metadata = Camera.Forward.X < 0 ? (byte)BlockFacing.East : (byte)BlockFacing.West;
                            }
                            else if (BlockRegistry.IsRailBlock(toPlace))
                            {
                                var hasNorth = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X, pb.Y, pb.Z - 1));
                                var hasSouth = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X, pb.Y, pb.Z + 1));
                                var hasWest = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X - 1, pb.Y, pb.Z));
                                var hasEast = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X + 1, pb.Y, pb.Z));

                                if (toPlace == BlockType.Rail)
                                {
                                    if (hasSouth && hasEast) metadata = (byte)RailShape.SouthEast;
                                    else if (hasSouth && hasWest) metadata = (byte)RailShape.SouthWest;
                                    else if (hasNorth && hasWest) metadata = (byte)RailShape.NorthWest;
                                    else if (hasNorth && hasEast) metadata = (byte)RailShape.NorthEast;
                                    else if (hasNorth || hasSouth) metadata = (byte)RailShape.NorthSouth;
                                    else if (hasEast || hasWest) metadata = (byte)RailShape.EastWest;
                                    else
                                        metadata = MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X)
                                            ? (byte)RailShape.NorthSouth
                                            : (byte)RailShape.EastWest;
                                }
                                else
                                {
                                    if (hasNorth || hasSouth) metadata = (byte)RailShape.NorthSouth;
                                    else if (hasEast || hasWest) metadata = (byte)RailShape.EastWest;
                                    else
                                        metadata = MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X)
                                            ? (byte)RailShape.NorthSouth
                                            : (byte)RailShape.EastWest;
                                }
                            }

                            world.SetBlockWorld(pb.X, pb.Y, pb.Z, toPlace, metadata);
                            if (toPlace is BlockType.Water or BlockType.Lava)
                                world.FluidSystem.TriggerFlow(world, pb, toPlace);
                            BlocksPlaced++;
                            AudioManager?.PlayBlockPlaceSound(toPlace);
                            Inventory.Slots[SelectedHotbarSlot].Count--;
                            if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                                Inventory.Slots[SelectedHotbarSlot].Clear();
                        }
                    }
                }
            }
        }

        rightClickWasDown = rightMouseDown;
    }

    private void UpdateHealthAndOxygen(World.World world, float deltaTime)
    {
        timeSinceLastDamage += deltaTime;
        if (DamageFlashTimer > 0f) DamageFlashTimer = MathF.Max(0f, DamageFlashTimer - deltaTime);

        var eyeX = (int)Math.Floor(Position.X);
        var eyeY = (int)Math.Floor(Position.Y + EyeHeight);
        var eyeZ = (int)Math.Floor(Position.Z);
        var eyeBlock = world.GetBlockWorld(eyeX, eyeY, eyeZ);
        var isHeadInWater = eyeBlock == (byte)BlockType.Water;

        if (isHeadInWater)
        {
            Oxygen = MathF.Max(0f, Oxygen - 6.67f * deltaTime);

            if (Oxygen <= 0f)
            {
                drownDamageTimer += deltaTime;
                if (drownDamageTimer >= 1.0f)
                {
                    drownDamageTimer = 0f;
                    TakeDamage(10.0f, "drown", world);
                    AudioManager?.PlayCategorySound("water", 0.8f, 0.15f);
                }
            }
            else
            {
                drownDamageTimer = 0f;
            }
        }
        else
        {
            Oxygen = MathF.Min(MaxOxygen, Oxygen + 40.0f * deltaTime);
            drownDamageTimer = 0f;
        }

        var footX = (int)Math.Floor(Position.X);
        var footY = (int)Math.Floor(Position.Y + 0.1f);
        var footZ = (int)Math.Floor(Position.Z);
        var bodyY = (int)Math.Floor(Position.Y + 0.9f);

        var isInLava = world.GetBlockWorld(footX, footY, footZ) == (byte)BlockType.Lava ||
                       world.GetBlockWorld(footX, bodyY, footZ) == (byte)BlockType.Lava ||
                       eyeBlock == (byte)BlockType.Lava;

        if (isInLava)
        {
            lavaDamageTimer += deltaTime;
            if (lavaDamageTimer >= 0.5f)
            {
                lavaDamageTimer = 0f;
                TakeDamage(15.0f, "lava", world);
            }
        }
        else
        {
            lavaDamageTimer = 0.45f;
        }

        var pBox = GetAABB();
        const float skin = 0.08f;
        var touchBox = new AABB(pBox.Min.X - skin, pBox.Min.Y - skin, pBox.Min.Z - skin,
            pBox.Max.X + skin, pBox.Max.Y + skin, pBox.Max.Z + skin);

        var minCX = (int)MathF.Floor(touchBox.Min.X - 0.5f);
        var maxCX = (int)MathF.Ceiling(touchBox.Max.X + 0.5f);
        var minCY = (int)MathF.Floor(touchBox.Min.Y - 0.5f);
        var maxCY = (int)MathF.Ceiling(touchBox.Max.Y + 0.5f);
        var minCZ = (int)MathF.Floor(touchBox.Min.Z - 0.5f);
        var maxCZ = (int)MathF.Ceiling(touchBox.Max.Z + 0.5f);

        var isTouchingCactus = false;
        for (var x = minCX; x <= maxCX; x++)
        for (var y = minCY; y <= maxCY; y++)
        for (var z = minCZ; z <= maxCZ; z++)
            if (world.GetBlockWorld(x, y, z) == (byte)BlockType.Cactus)
            {
                var cactusBox = new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f);
                if (touchBox.Intersects(cactusBox))
                {
                    isTouchingCactus = true;
                    goto DoneCactusCheck;
                }
            }

        DoneCactusCheck:

        if (isTouchingCactus)
        {
            cactusDamageTimer += deltaTime;
            if (cactusDamageTimer >= 0.5f)
            {
                cactusDamageTimer = 0f;
                TakeDamage(10.0f, "cactus", world);
            }
        }
        else
        {
            cactusDamageTimer = 0.45f;
        }

        if (Health < MaxHealth && Health > 0f && timeSinceLastDamage >= 10.0f && !isInLava && !isHeadInWater &&
            !isTouchingCactus) Health = MathF.Min(MaxHealth, Health + 6.0f * deltaTime);
    }

    public void TakeDamage(float amount, string damageType, World.World world)
    {
        if (amount <= 0f || Health <= 0f) return;

        Health = MathF.Max(0f, Health - amount);
        timeSinceLastDamage = 0f;
        DamageFlashTimer = 0.35f;

        AudioManager?.PlayPlayerHurt();

        if (Health <= 0f) DieAndRespawn(world);
    }

    public void DieAndRespawn(World.World world)
    {
        Health = MaxHealth;
        Oxygen = MaxOxygen;
        Stamina = MaxStamina;
        IsSprinting = false;
        Velocity = Vector3.Zero;
        IsFlying = false;
        FallDistance = 0f;
        timeSinceLastDamage = 10.0f;

        if (IsRiding) DismountMinecart();

        var spawnX = (int)Math.Floor(SpawnPosition.X);
        var spawnZ = (int)Math.Floor(SpawnPosition.Z);
        var spawnY = world.GetSurfaceHeight(spawnX, spawnZ);
        Position = new Vector3(spawnX + 0.5f, spawnY + 2.5f, spawnZ + 0.5f);
        UpdateCamera();
    }
}