using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VelmoraCraft.src.Physics;
using VelmoraCraft.src.World;

namespace VelmoraCraft.src.Entities
{
    public class Player
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; } = Vector3.Zero;

        // Player physical bounding box: 0.6 wide x 1.8 high
        public float Width { get; } = 0.6f;
        public float Height { get; } = 1.8f;
        public float EyeHeight { get; } = 1.62f;

        public bool IsGrounded { get; set; } = false;
        public bool IsFlying { get; set; } = false;

        public float MoveSpeed { get; set; } = 4.3f;
        public float SprintMultiplier { get; set; } = 1.4f;
        public float JumpForce { get; set; } = 7.5f;
        public float Gravity { get; set; } = 22.0f;

        public Camera Camera { get; }

        // Block interaction state
        public Vector3i? TargetedBlock { get; private set; }
        public Vector3i? PlacementBlock { get; private set; }
        public float BreakingProgress { get; private set; } = 0.0f; // 0.0 to 1.0
        public BlockType SelectedPlacementBlock => Inventory.Slots[SelectedHotbarSlot].Type;

        // Minecart riding state
        public bool IsRiding { get; private set; } = false;
        public Minecart? CurrentMinecart { get; private set; } = null;
        public Minecart? TargetedMinecart { get; private set; } = null;

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

        // Inventory & Hotbar (9 hotbar + 27 main = 36 slots)
        public UI.Inventory Inventory { get; } = new UI.Inventory(36);
        public int SelectedHotbarSlot { get; set; } = 0;
        public UI.ChestManager? ChestManager { get; set; }
        public Action<Vector3i, UI.Inventory>? OnOpenChest { get; set; }
        public Action? OnOpenCraftingTable { get; set; }
        public Action<Vector3i>? OnOpenFurnace { get; set; }
        public Audio.AudioManager? AudioManager { get; set; }
        public Action? OnPortalEntered { get; set; }
        public Action<Vector3i>? OnUseBed { get; set; }
        public Action? OnFlyToggled { get; set; }

        public int BlocksBroken { get; set; } = 0;
        public int BlocksPlaced { get; set; } = 0;
        private float footstepTimer = 0f;
        private bool wasInLiquid = false;
        private float waterSoundTimer = 0f;
        private float ladderSoundTimer = 0f;

        // Health, Oxygen & Survival state
        public float MaxHealth { get; set; } = 100.0f;
        public float Health { get; set; } = 100.0f;
        public float MaxOxygen { get; set; } = 100.0f;
        public float Oxygen { get; set; } = 100.0f;
        public float MaxStamina { get; set; } = 100.0f;
        public float Stamina { get; set; } = 100.0f;
        public bool IsSprinting { get; private set; } = false;
        public bool IsUnderwater { get; private set; } = false;
        public Vector3 SpawnPosition { get; set; }
        public float FallDistance { get; set; } = 0f;

        public float DamageFlashTimer { get; private set; } = 0f;
        private float drownDamageTimer = 0f;
        private float lavaDamageTimer = 0f;
        private float cactusDamageTimer = 0f;
        private float timeSinceLastDamage = 10.0f;

        private readonly List<AABB> candidateBoxes = new List<AABB>(64);
        public readonly List<ItemDrop> DroppedItems = new List<ItemDrop>();

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

        public void PopulateTestingInventory()
        {
            // Testing phase completed - starting inventory is empty for authentic gameplay
        }

        public void EnsureTestingItems()
        {
            // Testing phase completed
        }

        public AABB GetAABB()
        {
            float halfW = Width * 0.5f;
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
                // Dismount on Shift key
                if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                {
                    DismountMinecart();
                }
                else
                {
                    // Snap player to minecart seat
                    Position = CurrentMinecart.Position + new Vector3(0, 0.35f, 0);
                    Velocity = Vector3.Zero;
                    IsGrounded = true;

                    // Push / brake controls
                    Vector3 fwd = new Vector3(Camera.Forward.X, 0, Camera.Forward.Z);
                    if (fwd.LengthSquared > 0.001f) fwd = Vector3.Normalize(fwd);

                    if (keyboard.IsKeyDown(Keys.W))
                    {
                        CurrentMinecart.ApplyPush(fwd * 14.0f * deltaTime);
                    }
                    else if (keyboard.IsKeyDown(Keys.S))
                    {
                        CurrentMinecart.Velocity *= MathF.Pow(0.1f, deltaTime);
                    }

                    IsSprinting = false;
                    Camera.CurrentFov = MathHelper.Lerp(Camera.CurrentFov, Camera.BaseFov, Math.Min(1.0f, deltaTime * 8.0f));
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

            // Smooth FOV increase when sprinting (a little more than the set FOV, +10 degrees)
            float targetFov = IsSprinting ? (Camera.BaseFov + 10.0f) : Camera.BaseFov;
            Camera.CurrentFov = MathHelper.Lerp(Camera.CurrentFov, targetFov, Math.Min(1.0f, deltaTime * 8.0f));

            UpdateCamera();
            UpdateRaycast(world);
            HandleBlockInteractions(world, keyboard, mouse, deltaTime);
            RedstoneSystem.Update(world, deltaTime);
            UpdateItemDrops(world, deltaTime);
            UpdateHealthAndOxygen(world, deltaTime);

            // Check if player entered Nether Portal
            if (PortalSystem.IsPlayerInsidePortal(world, this))
            {
                OnPortalEntered?.Invoke();
            }
        }

        private void UpdateItemDrops(World.World world, float deltaTime)
        {
            for (int i = DroppedItems.Count - 1; i >= 0; i--)
            {
                var drop = DroppedItems[i];
                drop.Update(world, deltaTime);

                // Magnet / vacuum pickup if close to player
                if (drop.IsAlive && Vector3.Distance(drop.Position, Position + new Vector3(0, 0.9f, 0)) < 1.8f)
                {
                    if (Inventory.AddItem(drop.ItemType, drop.Count))
                    {
                        drop.IsAlive = false;
                    }
                }

                if (!drop.IsAlive)
                {
                    DroppedItems.RemoveAt(i);
                }
            }
        }

        private bool fKeyWasDown = false;
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
            float speed = MoveSpeed * 3.5f;
            if (keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl)) speed *= 2.0f;

            Vector3 move = Vector3.Zero;
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
            // Check if player is standing/swimming in water or lava
            int footX = (int)Math.Floor(Position.X);
            int footY = (int)Math.Floor(Position.Y + 0.2f);
            int footZ = (int)Math.Floor(Position.Z);
            int bodyY = (int)Math.Floor(Position.Y + 0.9f);
            byte footBlock = world.GetBlockWorld(footX, footY, footZ);
            byte bodyBlock = world.GetBlockWorld(footX, bodyY, footZ);
            bool inLiquid = (footBlock == (byte)BlockType.Water || footBlock == (byte)BlockType.Lava ||
                            bodyBlock == (byte)BlockType.Water || bodyBlock == (byte)BlockType.Lava);
            bool isWater = (footBlock == (byte)BlockType.Water || bodyBlock == (byte)BlockType.Water);

            // Eye / head submerged check
            int eyeX = (int)Math.Floor(Position.X);
            int eyeY = (int)Math.Floor(Position.Y + EyeHeight);
            int eyeZ = (int)Math.Floor(Position.Z);
            byte eyeBlock = world.GetBlockWorld(eyeX, eyeY, eyeZ);
            bool isHeadInWater = (eyeBlock == (byte)BlockType.Water);
            IsUnderwater = isHeadInWater || (isWater && Oxygen < MaxOxygen * 0.99f);

            // Splash sound on entering water
            if (inLiquid && !wasInLiquid)
            {
                if (isWater)
                {
                    AudioManager?.PlayCategorySound("water", volume: 0.85f, pitchVariance: 0.15f);
                }
            }
            wasInLiquid = inLiquid;

            // Planar movement (horizontal only)
            Vector3 forwardPlanar = Vector3.Normalize(new Vector3(Camera.Forward.X, 0, Camera.Forward.Z));
            Vector3 rightPlanar = Vector3.Normalize(new Vector3(Camera.Right.X, 0, Camera.Right.Z));

            Vector3 wishDir = Vector3.Zero;
            if (keyboard.IsKeyDown(Keys.W)) wishDir += forwardPlanar;
            if (keyboard.IsKeyDown(Keys.S)) wishDir -= forwardPlanar;
            if (keyboard.IsKeyDown(Keys.D)) wishDir += rightPlanar;
            if (keyboard.IsKeyDown(Keys.A)) wishDir -= rightPlanar;

            if (wishDir.LengthSquared > 0.001f)
            {
                wishDir = Vector3.Normalize(wishDir);
            }

            // Sprint key handling (Ctrl key only)
            bool sprintKey = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            bool isMovingHorizontally = wishDir.LengthSquared > 0.001f;
            float speed = MoveSpeed;

            if (inLiquid)
            {
                // Underwater: sprint is unlimited! Stamina does not deplete
                IsSprinting = sprintKey && (isMovingHorizontally || keyboard.IsKeyDown(Keys.Space) || keyboard.IsKeyDown(Keys.LeftShift));
                if (IsSprinting)
                {
                    speed *= SprintMultiplier;
                }
                // Passive stamina recovery while in water
                Stamina = MathF.Min(MaxStamina, Stamina + (MaxStamina / 8.0f) * deltaTime);
                speed *= 0.65f;
            }
            else
            {
                // On land: sprint lasts 30s and recharges in 8s
                if (sprintKey && isMovingHorizontally && Stamina > 0.5f)
                {
                    IsSprinting = true;
                    speed *= SprintMultiplier;
                    // Drain stamina from 100 to 0 over 30 seconds
                    Stamina = MathF.Max(0f, Stamina - (MaxStamina / 30.0f) * deltaTime);
                }
                else
                {
                    IsSprinting = false;
                    // Recharge stamina from 0 to 100 over 8 seconds
                    Stamina = MathF.Min(MaxStamina, Stamina + (MaxStamina / 8.0f) * deltaTime);
                }
            }

            // Apply horizontal acceleration
            Vector3 targetVel = wishDir * speed;
            float horizontalLerp = inLiquid ? 6.0f : (IsGrounded ? 12.0f : 2.5f);
            float vx = MathHelper.Lerp(Velocity.X, targetVel.X, horizontalLerp * deltaTime);
            float vz = MathHelper.Lerp(Velocity.Z, targetVel.Z, horizontalLerp * deltaTime);

            float vy;
            if (inLiquid)
            {
                // Liquid buoyancy: reduced gravity and swimming controls
                vy = MathHelper.Lerp(Velocity.Y, -1.8f, 4.0f * deltaTime);
                if (keyboard.IsKeyDown(Keys.Space))
                {
                    vy = IsSprinting ? 5.0f : 3.8f; // Swim upwards faster with sprint
                }
                else if (keyboard.IsKeyDown(Keys.LeftShift))
                {
                    vy = IsSprinting ? -5.0f : -3.8f; // Dive downwards faster with sprint
                }

                FallDistance = 0f; // Liquid cushions and prevents all fall damage
                Velocity = new Vector3(vx, vy, vz);

                // Play water swimming / paddling sound
                if (isWater)
                {
                    bool isMoving = (wishDir.LengthSquared > 0.001f) || Math.Abs(Velocity.Y) > 0.6f;
                    if (isMoving)
                    {
                        waterSoundTimer += deltaTime;
                        if (waterSoundTimer > 0.42f)
                        {
                            waterSoundTimer = 0f;
                            AudioManager?.PlayCategorySound("water", volume: 0.70f, pitchVariance: 0.15f);
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

            // Check ladder interaction (bounding box query)
            bool onLadder = false;
            int ladderMinX = (int)Math.Floor(Position.X - Width * 0.5f);
            int ladderMaxX = (int)Math.Floor(Position.X + Width * 0.5f);
            int ladderMinY = (int)Math.Floor(Position.Y);
            int ladderMaxY = (int)Math.Floor(Position.Y + Height);
            int ladderMinZ = (int)Math.Floor(Position.Z - Width * 0.5f);
            int ladderMaxZ = (int)Math.Floor(Position.Z + Width * 0.5f);

            for (int lx = ladderMinX; lx <= ladderMaxX && !onLadder; lx++)
            {
                for (int ly = ladderMinY; ly <= ladderMaxY && !onLadder; ly++)
                {
                    for (int lz = ladderMinZ; lz <= ladderMaxZ && !onLadder; lz++)
                    {
                        if (world.GetBlockWorld(lx, ly, lz) == (byte)BlockType.Ladder)
                        {
                            onLadder = true;
                        }
                    }
                }
            }

            if (onLadder)
            {
                float ladderVy = 0f;
                if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Space))
                {
                    ladderVy = 3.2f; // Climb up ladder
                }
                else if (keyboard.IsKeyDown(Keys.S))
                {
                    ladderVy = -2.8f; // Climb down ladder
                }
                else if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                {
                    ladderVy = 0f; // Hold position / stationary hang
                }
                else
                {
                    ladderVy = -1.0f; // Gentle slow slide
                }

                FallDistance = 0f; // Ladders cushion all fall damage
                Velocity = new Vector3(vx * 0.6f, ladderVy, vz * 0.6f);

                if (Math.Abs(ladderVy) > 0.4f || (vx * vx + vz * vz > 0.1f))
                {
                    ladderSoundTimer += deltaTime;
                    if (ladderSoundTimer > 0.35f)
                    {
                        ladderSoundTimer = 0f;
                        AudioManager?.PlayCategorySound("wood", volume: 0.5f, pitchVariance: 0.1f);
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

            bool wasGrounded = IsGrounded;
            float startY = Position.Y;

            // Apply normal gravity
            vy = Velocity.Y - (Gravity * deltaTime);

            // Jumping
            if (IsGrounded && keyboard.IsKeyDown(Keys.Space))
            {
                vy = JumpForce;
                IsGrounded = false;
                AudioManager?.PlayPlayerJump();
            }

            Velocity = new Vector3(vx, vy, vz);

            // Move and collide against voxel terrain
            MoveWithCollision(world, Velocity * deltaTime);
            ClampToWorldBounds();

            float endY = Position.Y;
            float dropThisFrame = startY - endY;

            if (dropThisFrame > 0f)
            {
                FallDistance += dropThisFrame;
            }
            else if (Velocity.Y > 0f)
            {
                // Jumping upwards or climbing up, reset fall counter
                FallDistance = 0f;
            }

            // Landing sound & Fall Damage
            if (!wasGrounded && IsGrounded)
            {
                AudioManager?.PlayPlayerLand();

                // Fall damage occurs only after falling more than 3 blocks (say after the height of 3 blocks)
                if (FallDistance > 3.0f)
                {
                    float excessBlocks = FallDistance - 3.0f;
                    float damage = excessBlocks * 10.0f;
                    TakeDamage(damage, "fall", world);
                }
                FallDistance = 0f;
            }
            else if (IsGrounded)
            {
                FallDistance = 0f;
            }

            // Footstep sounds
            if (IsGrounded && (vx * vx + vz * vz > 0.4f))
            {
                footstepTimer += deltaTime;
                if (footstepTimer > 0.38f)
                {
                    footstepTimer = 0f;
                    int groundX = (int)Math.Floor(Position.X);
                    int groundY = (int)Math.Floor(Position.Y - 0.2f);
                    int groundZ = (int)Math.Floor(Position.Z);
                    byte groundBlock = world.GetBlockWorld(groundX, groundY, groundZ);

                    // If wading in shallow water, play water sound
                    if (footBlock == (byte)BlockType.Water)
                    {
                        AudioManager?.PlayCategorySound("water", volume: 0.60f, pitchVariance: 0.15f);
                    }
                    else
                    {
                        AudioManager?.PlayBlockStepSound((BlockType)groundBlock);
                    }
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

            // Up to 4 passes to resolve compound overlaps (e.g. slopes, corners, steps)
            for (int iteration = 0; iteration < 4; iteration++)
            {
                AABB pBox = GetAABB();
                int minX = (int)MathF.Floor(pBox.Min.X);
                int maxX = (int)MathF.Ceiling(pBox.Max.X);
                int minY = (int)MathF.Floor(pBox.Min.Y);
                int maxY = (int)MathF.Ceiling(pBox.Max.Y);
                int minZ = (int)MathF.Floor(pBox.Min.Z);
                int maxZ = (int)MathF.Ceiling(pBox.Max.Z);

                float pushUpTarget = float.MinValue;
                bool isIntersecting = false;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            byte b = world.GetBlockWorld(x, y, z);
                            if (BlockRegistry.IsSolid(b))
                            {
                                AABB blockBox = new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f);
                                if (pBox.Intersects(blockBox))
                                {
                                    isIntersecting = true;
                                    // If block intersects player's feet or lower body
                                    if (blockBox.Max.Y > pBox.Min.Y && blockBox.Max.Y <= pBox.Max.Y + 0.1f)
                                    {
                                        if (blockBox.Max.Y > pushUpTarget)
                                        {
                                            pushUpTarget = blockBox.Max.Y;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!isIntersecting || pushUpTarget <= float.MinValue)
                {
                    break;
                }

                Position = new Vector3(Position.X, pushUpTarget, Position.Z);
                Velocity = new Vector3(Velocity.X, MathF.Max(0, Velocity.Y), Velocity.Z);
                IsGrounded = true;
            }

            UpdateCamera();
        }

        private void MoveWithCollision(World.World world, Vector3 movement)
        {
            if (!IsFlying)
            {
                ResolveStuckInBlocks(world);
            }

            AABB playerBox = GetAABB();

            // Broad-phase query surrounding blocks
            int minX = (int)MathF.Floor(playerBox.Min.X + MathF.Min(0, movement.X) - 0.5f);
            int maxX = (int)MathF.Ceiling(playerBox.Max.X + MathF.Max(0, movement.X) + 0.5f);
            int minY = (int)MathF.Floor(playerBox.Min.Y + MathF.Min(0, movement.Y) - 0.5f);
            int maxY = (int)MathF.Ceiling(playerBox.Max.Y + MathF.Max(0, movement.Y) + 0.5f);
            int minZ = (int)MathF.Floor(playerBox.Min.Z + MathF.Min(0, movement.Z) - 0.5f);
            int maxZ = (int)MathF.Ceiling(playerBox.Max.Z + MathF.Max(0, movement.Z) + 0.5f);

            candidateBoxes.Clear();
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        byte b = world.GetBlockWorld(x, y, z);
                        if (BlockRegistry.IsSolid(b))
                        {
                            candidateBoxes.Add(new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f));
                        }
                    }
                }
            }

            bool hitGroundThisFrame = false;

            // Y axis collision
            if (movement.Y != 0)
            {
                foreach (var box in candidateBoxes)
                {
                    if (playerBox.Min.X < box.Max.X && playerBox.Max.X > box.Min.X &&
                        playerBox.Min.Z < box.Max.Z && playerBox.Max.Z > box.Min.Z)
                    {
                        if (movement.Y > 0 && playerBox.Max.Y <= box.Min.Y && playerBox.Max.Y + movement.Y > box.Min.Y)
                        {
                            movement.Y = box.Min.Y - playerBox.Max.Y;
                            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
                        }
                        else if (movement.Y < 0 && playerBox.Min.Y >= box.Max.Y - 0.5f && playerBox.Min.Y + movement.Y <= box.Max.Y && playerBox.Max.Y > box.Max.Y)
                        {
                            movement.Y = box.Max.Y - playerBox.Min.Y;
                            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
                            hitGroundThisFrame = true;
                        }
                    }
                }
                Position = new Vector3(Position.X, Position.Y + movement.Y, Position.Z);
                playerBox = GetAABB();
            }

            if (hitGroundThisFrame)
            {
                IsGrounded = true;
            }
            else if (movement.Y < -0.001f || movement.Y > 0.001f)
            {
                IsGrounded = false;
            }

            // X axis collision
            if (movement.X != 0)
            {
                foreach (var box in candidateBoxes)
                {
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
                }
                Position = new Vector3(Position.X + movement.X, Position.Y, Position.Z);
                playerBox = GetAABB();
            }

            // Z axis collision
            if (movement.Z != 0)
            {
                foreach (var box in candidateBoxes)
                {
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
                }
                Position = new Vector3(Position.X, Position.Y, Position.Z + movement.Z);
            }
        }

        private void ClampToWorldBounds()
        {
            Vector3 pos = Position;
            pos.X = Math.Clamp(pos.X, 1.0f, (float)World.World.WORLD_SIZE_X - 1.0f);
            pos.Z = Math.Clamp(pos.Z, 1.0f, (float)World.World.WORLD_SIZE_Z - 1.0f);
            pos.Y = Math.Clamp(pos.Y, 1.0f, 125.0f);
            Position = pos;
        }

        private void UpdateCamera()
        {
            Camera.Position = Position + new Vector3(0, EyeHeight, 0);
        }

        private void UpdateRaycast(World.World world)
        {
            var hit = VoxelRaycaster.Raycast(world, Camera.Position, Camera.Forward, maxDistance: 5.5f);
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

            // Raycast against Minecart entities
            TargetedMinecart = null;
            float closestDist = hit.HasHit ? hit.Distance : 5.5f;

            lock (world.Minecarts)
            {
                for (int i = 0; i < world.Minecarts.Count; i++)
                {
                    var cart = world.Minecarts[i];
                    if (cart == null || !cart.IsAlive || cart == CurrentMinecart) continue;

                    if (cart.GetAABB().IntersectsRay(Camera.Position, Camera.Forward, 5.5f, out float cartDist))
                    {
                        if (cartDist < closestDist)
                        {
                            closestDist = cartDist;
                            TargetedMinecart = cart;
                        }
                    }
                }
            }
        }

        private bool rightClickWasDown = false;
        private void HandleBlockInteractions(World.World world, KeyboardState keyboard, MouseState mouse, float deltaTime)
        {
            // 0. Interaction with Minecart Entities
            if (TargetedMinecart != null)
            {
                // Left Click -> Break minecart (drop item)
                if (mouse.IsButtonDown(MouseButton.Left) && !IsRiding)
                {
                    TargetedMinecart.IsAlive = false;
                    lock (world.Minecarts)
                    {
                        world.Minecarts.Remove(TargetedMinecart);
                    }
                    DroppedItems.Add(new ItemDrop(BlockType.MinecartItem, TargetedMinecart.Position + new Vector3(0, 0.2f, 0)));
                    TargetedMinecart = null;
                    return;
                }

                // Right Click -> Mount minecart!
                if (mouse.IsButtonDown(MouseButton.Right) && !rightClickWasDown)
                {
                    if (!IsRiding)
                    {
                        MountMinecart(TargetedMinecart);
                        rightClickWasDown = true;
                        return;
                    }
                }
            }

            // Right click while riding -> Dismount
            if (IsRiding && mouse.IsButtonDown(MouseButton.Right) && !rightClickWasDown)
            {
                DismountMinecart();
                rightClickWasDown = true;
                return;
            }

            // 1. Timed Block Breaking (Left Click)
            if (mouse.IsButtonDown(MouseButton.Left) && TargetedBlock.HasValue)
            {
                var bp = TargetedBlock.Value;
                byte blockId = world.GetBlockWorld(bp.X, bp.Y, bp.Z);
                var def = BlockRegistry.Get(blockId);

                if (def.Hardness >= 0) // Bedrock is -1 (unbreakable)
                {
                    float breakTime = MathF.Max(0.15f, def.Hardness * 0.8f);
                    BreakingProgress += deltaTime / breakTime;

                    if (BreakingProgress >= 1.0f)
                    {
                        // Block Broken!
                        world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Empty);
                        world.FluidSystem.OnBlockBroken(world, bp);
                        BlocksBroken++;
                        AudioManager?.PlayBlockBreakSound(def.Type);

                        BlockType dropType = def.Type;

                        // Authentic drops: mining Stone yields Cobblestone, CoalOre yields Coal
                        if (def.Type == BlockType.Stone)
                        {
                            dropType = BlockType.Cobblestone;
                        }
                        else if (def.Type == BlockType.CoalOre)
                        {
                            dropType = BlockType.Coal;
                        }

                        // Normalize drops and clean paired blocks
                        if (def.Type == BlockType.DoorLower || def.Type == BlockType.DoorLowerOpen)
                        {
                            dropType = BlockType.DoorLower;
                            var up = bp + Vector3i.UnitY;
                            byte upId = world.GetBlockWorld(up.X, up.Y, up.Z);
                            if (upId == (byte)BlockType.DoorUpper || upId == (byte)BlockType.DoorUpperOpen)
                            {
                                world.SetBlockWorld(up.X, up.Y, up.Z, BlockType.Empty);
                            }
                        }
                        else if (def.Type == BlockType.DoorUpper || def.Type == BlockType.DoorUpperOpen)
                        {
                            dropType = BlockType.DoorLower;
                            var down = bp - Vector3i.UnitY;
                            byte downId = world.GetBlockWorld(down.X, down.Y, down.Z);
                            if (downId == (byte)BlockType.DoorLower || downId == (byte)BlockType.DoorLowerOpen)
                            {
                                world.SetBlockWorld(down.X, down.Y, down.Z, BlockType.Empty);
                            }
                        }
                        else if (def.Type == BlockType.IronDoorLower || def.Type == BlockType.IronDoorLowerOpen)
                        {
                            dropType = BlockType.IronDoorLower;
                            var up = bp + Vector3i.UnitY;
                            byte upId = world.GetBlockWorld(up.X, up.Y, up.Z);
                            if (upId == (byte)BlockType.IronDoorUpper || upId == (byte)BlockType.IronDoorUpperOpen)
                            {
                                world.SetBlockWorld(up.X, up.Y, up.Z, BlockType.Empty);
                            }
                        }
                        else if (def.Type == BlockType.IronDoorUpper || def.Type == BlockType.IronDoorUpperOpen)
                        {
                            dropType = BlockType.IronDoorLower;
                            var down = bp - Vector3i.UnitY;
                            byte downId = world.GetBlockWorld(down.X, down.Y, down.Z);
                            if (downId == (byte)BlockType.IronDoorLower || downId == (byte)BlockType.IronDoorLowerOpen)
                            {
                                world.SetBlockWorld(down.X, down.Y, down.Z, BlockType.Empty);
                            }
                        }
                        else if (def.Type == BlockType.LeverOn)
                        {
                            dropType = BlockType.Lever;
                            RedstoneSystem.PropagatePower(world, bp, isPowered: false);
                        }
                        else if (def.Type == BlockType.StoneButtonPressed)
                        {
                            dropType = BlockType.StoneButton;
                            RedstoneSystem.PropagatePower(world, bp, isPowered: false);
                        }
                        else if (def.Type == BlockType.PoweredRailOn)
                        {
                            dropType = BlockType.PoweredRail;
                        }
                        else if (def.Type == BlockType.BedFoot || def.Type == BlockType.BedHead)
                        {
                            dropType = BlockType.BedFoot;
                            // Clean up the other half of the bed
                            Vector3i[] bedOffsets = { Vector3i.UnitX, -Vector3i.UnitX, Vector3i.UnitZ, -Vector3i.UnitZ };
                            foreach (var off in bedOffsets)
                            {
                                var nPos = bp + off;
                                byte nId = world.GetBlockWorld(nPos.X, nPos.Y, nPos.Z);
                                if (nId == (byte)BlockType.BedFoot || nId == (byte)BlockType.BedHead)
                                {
                                    world.SetBlockWorld(nPos.X, nPos.Y, nPos.Z, BlockType.Empty);
                                    break;
                                }
                            }
                        }

                        if (def.Type == BlockType.Log || def.Type == BlockType.BirchLog || def.Type == BlockType.SpruceLog)
                        {
                            world.TriggerTreeLeafDecay(bp);
                            DroppedItems.Add(new ItemDrop(dropType, new Vector3(bp.X, bp.Y, bp.Z)));
                        }
                        else if (def.Type == BlockType.OakSapling)
                        {
                            world.GrowingSaplings.TryRemove(bp, out _);
                            DroppedItems.Add(new ItemDrop(BlockType.OakSapling, new Vector3(bp.X, bp.Y, bp.Z)));
                        }
                        else if (def.Type == BlockType.Leaves || def.Type == BlockType.SpruceLeaves)
                        {
                            world.PendingDecayingLeaves.TryRemove(bp, out _);
                            // Leaves have a ~20% chance to drop Oak Sapling when broken by hand
                            if (Random.Shared.Next(100) < 20)
                            {
                                DroppedItems.Add(new ItemDrop(BlockType.OakSapling, new Vector3(bp.X, bp.Y, bp.Z)));
                            }
                        }
                        else if (def.Type == BlockType.TallGrass)
                        {
                            // Tall grass has a ~60% chance to drop Wheat Seeds and ~20% chance to drop plant fibers/Cobwebs
                            int roll = Random.Shared.Next(100);
                            if (roll < 60)
                            {
                                DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z)));
                            }
                            if (roll < 20)
                            {
                                DroppedItems.Add(new ItemDrop(BlockType.Cobweb, new Vector3(bp.X, bp.Y, bp.Z)));
                            }
                        }
                        else if (def.Type == BlockType.Wheat)
                        {
                            byte stage = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            world.GrowingCrops.Remove(bp);
                            world.CropStageTimers.TryRemove(bp, out _);
                            if (stage >= 7)
                            {
                                // Fully grown wheat drops harvested Wheat item + 1-2 Wheat Seeds
                                DroppedItems.Add(new ItemDrop(BlockType.Wheat, new Vector3(bp.X, bp.Y, bp.Z)));
                                int seeds = 1 + Random.Shared.Next(2);
                                DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z), seeds));
                            }
                            else
                            {
                                // Immature wheat only returns 1 seed
                                DroppedItems.Add(new ItemDrop(BlockType.WheatSeeds, new Vector3(bp.X, bp.Y, bp.Z)));
                            }
                        }
                        else
                        {
                            DroppedItems.Add(new ItemDrop(dropType, new Vector3(bp.X, bp.Y, bp.Z)));
                        }

                        // If broken block was a chest, drop its contents
                        if (def.Type == BlockType.Chest && ChestManager != null)
                        {
                            var chestInv = ChestManager.GetOrCreateChest(bp);
                            for (int ci = 0; ci < chestInv.SlotCount; ci++)
                            {
                                if (!chestInv.Slots[ci].IsEmpty)
                                {
                                    DroppedItems.Add(new ItemDrop(chestInv.Slots[ci].Type, new Vector3(bp.X, bp.Y, bp.Z), chestInv.Slots[ci].Count));
                                    chestInv.Slots[ci].Clear();
                                }
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

            // 2. Direct Interaction / Block Placement (Right Click)
            bool rightMouseDown = mouse.IsButtonDown(MouseButton.Right);
            if (rightMouseDown && !rightClickWasDown)
            {
                var hotbarStack = Inventory.Slots[SelectedHotbarSlot];

                // If holding FlintAndSteel: try igniting Nether Portal!
                if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.FlintAndSteel)
                {
                    bool ignited = false;
                    if (TargetedBlock.HasValue && PortalSystem.TryIgnitePortal(world, TargetedBlock.Value))
                    {
                        ignited = true;
                    }
                    else if (PlacementBlock.HasValue && PortalSystem.TryIgnitePortal(world, PlacementBlock.Value))
                    {
                        ignited = true;
                    }

                    if (ignited)
                    {
                        AudioManager?.PlayPortalIgnite();
                        rightClickWasDown = true;
                        return;
                    }
                }

                // If holding MinecartItem and aiming at a rail track: spawn minecart!
                if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.MinecartItem && TargetedBlock.HasValue)
                {
                    var bp = TargetedBlock.Value;
                    byte b = world.GetBlockWorld(bp.X, bp.Y, bp.Z);
                    if (b == (byte)BlockType.Rail || b == (byte)BlockType.PoweredRail || b == (byte)BlockType.PoweredRailOn)
                    {
                        var cart = new Minecart(new Vector3(bp.X, bp.Y + 0.1f, bp.Z));
                        lock (world.Minecarts)
                        {
                            world.Minecarts.Add(cart);
                        }
                        Inventory.Slots[SelectedHotbarSlot].Count--;
                        if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                        {
                            Inventory.Slots[SelectedHotbarSlot].Clear();
                        }
                        rightClickWasDown = true;
                        return;
                    }
                }
                // First check direct interactions on the targeted block
                if (TargetedBlock.HasValue)
                {
                    var bp = TargetedBlock.Value;
                    byte blockId = world.GetBlockWorld(bp.X, bp.Y, bp.Z);

                    bool isShiftDown = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

                    // In-place Block Rotation (Shift + Right Click on placed directional block or rail)
                    if (isShiftDown)
                    {
                        if (blockId == (byte)BlockType.Chest)
                        {
                            if (VelmoraCraft.src.UI.ChestManager.TryGetDoubleChestPartner(bp, (x, y, z) => world.GetBlockWorld(x, y, z), out var partnerPos, out _, out bool isAxisX))
                            {
                                byte currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                                byte nextMeta;
                                if (isAxisX)
                                {
                                    nextMeta = (currentMeta == (byte)BlockFacing.South) ? (byte)BlockFacing.North : (byte)BlockFacing.South;
                                }
                                else
                                {
                                    nextMeta = (currentMeta == (byte)BlockFacing.West) ? (byte)BlockFacing.East : (byte)BlockFacing.West;
                                }
                                world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                                world.SetBlockMetadata(partnerPos.X, partnerPos.Y, partnerPos.Z, nextMeta);
                                AudioManager?.PlayCategorySound("wood", volume: 0.5f, pitchVariance: 0.1f);
                                rightClickWasDown = true;
                                return;
                            }
                            else
                            {
                                byte currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                                byte nextMeta = (byte)((currentMeta + 1) % 4);
                                world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                                AudioManager?.PlayCategorySound("wood", volume: 0.5f, pitchVariance: 0.1f);
                                rightClickWasDown = true;
                                return;
                            }
                        }
                        else if (BlockRegistry.IsDoorBlock(blockId))
                        {
                            Vector3i otherPos = bp;
                            if (blockId == (byte)BlockType.DoorLower || blockId == (byte)BlockType.DoorLowerOpen ||
                                blockId == (byte)BlockType.IronDoorLower || blockId == (byte)BlockType.IronDoorLowerOpen)
                            {
                                otherPos = bp + Vector3i.UnitY;
                            }
                            else
                            {
                                otherPos = bp - Vector3i.UnitY;
                            }
                            byte currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            byte nextMeta = (byte)((currentMeta + 1) % 4);
                            world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                            if (BlockRegistry.IsDoorBlock(world.GetBlockWorld(otherPos.X, otherPos.Y, otherPos.Z)))
                            {
                                world.SetBlockMetadata(otherPos.X, otherPos.Y, otherPos.Z, nextMeta);
                            }
                            AudioManager?.PlayCategorySound("wood", volume: 0.5f, pitchVariance: 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                        else if (BlockRegistry.IsDirectionalBlock((BlockType)blockId))
                        {
                            byte currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            byte nextMeta = (byte)((currentMeta + 1) % 4);
                            world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                            AudioManager?.PlayCategorySound("wood", volume: 0.5f, pitchVariance: 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                        else if (BlockRegistry.IsRailBlock(blockId))
                        {
                            byte currentMeta = world.GetBlockMetadata(bp.X, bp.Y, bp.Z);
                            int maxShapes = (blockId == (byte)BlockType.Rail) ? 6 : 2;
                            byte nextMeta = (byte)((currentMeta + 1) % maxShapes);
                            world.SetBlockMetadata(bp.X, bp.Y, bp.Z, nextMeta);
                            AudioManager?.PlayCategorySound("stone", volume: 0.5f, pitchVariance: 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                        else if (blockId == (byte)BlockType.Grass || blockId == (byte)BlockType.Dirt)
                        {
                            // Tilling: Shift-Right-Click grass or dirt block next to water turns it into Farmland
                            bool hasWater = false;
                            for (int dx = -2; dx <= 2 && !hasWater; dx++)
                            {
                                for (int dz = -2; dz <= 2 && !hasWater; dz++)
                                {
                                    for (int dy = -1; dy <= 1; dy++)
                                    {
                                        if (world.GetBlockWorld(bp.X + dx, bp.Y + dy, bp.Z + dz) == (byte)BlockType.Water)
                                        {
                                            hasWater = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (hasWater)
                            {
                                world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Farmland);
                                AudioManager?.PlayCategorySound("gravel", volume: 0.8f, pitchVariance: 0.15f);
                                rightClickWasDown = true;
                                return;
                            }
                        }
                    }

                    // A. Chest interaction
                    if (blockId == (byte)BlockType.Chest)
                    {
                        if (ChestManager != null && OnOpenChest != null)
                        {
                            var chestInv = ChestManager.GetChestInventory(bp, world);
                            OnOpenChest.Invoke(bp, chestInv);
                            AudioManager?.PlayCategorySound("wood", volume: 0.8f, pitchVariance: 0.1f);
                            rightClickWasDown = true;
                            return;
                        }
                    }

                    // A2. Crafting Table interaction
                    if (blockId == (byte)BlockType.CraftingTable)
                    {
                        if (OnOpenCraftingTable != null)
                        {
                            OnOpenCraftingTable.Invoke();
                            AudioManager?.PlayCategorySound("wood", volume: 0.7f);
                            rightClickWasDown = true;
                            return;
                        }
                    }

                    // A3. Furnace interaction (LitFurnace is purely decorative)
                    if (blockId == (byte)BlockType.Furnace)
                    {
                        if (OnOpenFurnace != null)
                        {
                            OnOpenFurnace.Invoke(bp);
                            AudioManager?.PlayCategorySound("concrete", volume: 0.7f);
                            rightClickWasDown = true;
                            return;
                        }
                    }

                    // B. Door interaction (open/close)
                    if (blockId == (byte)BlockType.DoorLower || blockId == (byte)BlockType.DoorUpper ||
                        blockId == (byte)BlockType.DoorLowerOpen || blockId == (byte)BlockType.DoorUpperOpen)
                    {
                        RedstoneSystem.ToggleDoor(world, bp);
                        AudioManager?.PlayBlockPlaceSound(BlockType.Planks);
                        rightClickWasDown = true;
                        return;
                    }

                    if (blockId == (byte)BlockType.IronDoorLower || blockId == (byte)BlockType.IronDoorUpper ||
                        blockId == (byte)BlockType.IronDoorLowerOpen || blockId == (byte)BlockType.IronDoorUpperOpen)
                    {
                        RedstoneSystem.ToggleDoor(world, bp);
                        AudioManager?.PlayBlockPlaceSound(BlockType.IronBlock);
                        rightClickWasDown = true;
                        return;
                    }

                    // C. Lever interaction (flip on/off)
                    if (blockId == (byte)BlockType.Lever || blockId == (byte)BlockType.LeverOn)
                    {
                        RedstoneSystem.ToggleLever(world, bp);
                        AudioManager?.PlayBlockPlaceSound(BlockType.Cobblestone);
                        rightClickWasDown = true;
                        return;
                    }

                    // D. Stone Button interaction (press)
                    if (blockId == (byte)BlockType.StoneButton)
                    {
                        RedstoneSystem.PressButton(world, bp);
                        AudioManager?.PlayBlockPlaceSound(BlockType.Stone);
                        rightClickWasDown = true;
                        return;
                    }

                    // E. Bed interaction (sleep / skip night)
                    if (blockId == (byte)BlockType.BedFoot || blockId == (byte)BlockType.BedHead)
                    {
                        OnUseBed?.Invoke(bp);
                        rightClickWasDown = true;
                        return;
                    }

                    // F. Farmland interaction (plant WheatSeeds directly on farmland)
                    if (blockId == (byte)BlockType.Farmland && !hotbarStack.IsEmpty && hotbarStack.Type == BlockType.WheatSeeds)
                    {
                        var plantPos = bp + Vector3i.UnitY;
                        if (plantPos.Y < Chunk.SIZE_Y && world.GetBlockWorld(plantPos.X, plantPos.Y, plantPos.Z) == (byte)BlockType.Empty)
                        {
                            world.SetBlockWorld(plantPos.X, plantPos.Y, plantPos.Z, BlockType.Wheat);
                            world.SetBlockMetadata(plantPos.X, plantPos.Y, plantPos.Z, 0); // Stage 0
                            world.GrowingCrops.Add(plantPos);
                            world.CropStageTimers[plantPos] = 25.0f + Random.Shared.NextSingle() * 10.0f;
                            AudioManager?.PlayBlockPlaceSound(BlockType.Grass);
                            Inventory.Slots[SelectedHotbarSlot].Count--;
                            if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                            {
                                Inventory.Slots[SelectedHotbarSlot].Clear();
                            }
                            rightClickWasDown = true;
                            return;
                        }
                    }

                    // G. Cake interaction (eat cake slice to restore health and stamina)
                    if (blockId == (byte)BlockType.Cake)
                    {
                        if (Health < MaxHealth || Stamina < MaxStamina)
                        {
                            Health = MathF.Min(MaxHealth, Health + 25.0f);
                            Stamina = MathF.Min(MaxStamina, Stamina + 30.0f);
                            world.SetBlockWorld(bp.X, bp.Y, bp.Z, BlockType.Empty);
                            AudioManager?.PlayCategorySound("cloth", volume: 0.9f);
                            rightClickWasDown = true;
                            return;
                        }
                    }
                }

                // Food consumption: Bread in hand
                if (!hotbarStack.IsEmpty && hotbarStack.Type == BlockType.Bread)
                {
                    if (Health < MaxHealth || Stamina < MaxStamina)
                    {
                        Health = MathF.Min(MaxHealth, Health + 25.0f);
                        Stamina = MathF.Min(MaxStamina, Stamina + 25.0f);
                        Inventory.Slots[SelectedHotbarSlot].Count--;
                        if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                        {
                            Inventory.Slots[SelectedHotbarSlot].Clear();
                        }
                        AudioManager?.PlayCategorySound("wood", volume: 0.8f);
                        rightClickWasDown = true;
                        return;
                    }
                }

                // Normal block placement from hotbar
                if (PlacementBlock.HasValue)
                {
                    var pb = PlacementBlock.Value;
                    hotbarStack = Inventory.Slots[SelectedHotbarSlot];

                    if (!hotbarStack.IsEmpty && hotbarStack.Type != BlockType.Empty)
                    {
                        var placeDef = BlockRegistry.Get(hotbarStack.Type);

                        // Special multi-block placement: Wooden or Iron Door (2 blocks tall)
                        if (hotbarStack.Type == BlockType.DoorLower || hotbarStack.Type == BlockType.IronDoorLower)
                        {
                            if (pb.Y + 1 < Chunk.SIZE_Y && world.GetBlockWorld(pb.X, pb.Y + 1, pb.Z) == (byte)BlockType.Empty)
                            {
                                AABB lowerBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.5f, pb.Z + 0.5f);
                                AABB upperBox = new AABB(pb.X - 0.5f, pb.Y + 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 1.5f, pb.Z + 0.5f);
                                AABB pBox = GetAABB();

                                if (!pBox.Intersects(lowerBox) && !pBox.Intersects(upperBox))
                                {
                                    bool isIron = hotbarStack.Type == BlockType.IronDoorLower;
                                    byte facing;
                                    if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                    {
                                        facing = Camera.Forward.Z < 0 ? (byte)BlockFacing.South : (byte)BlockFacing.North;
                                    }
                                    else
                                    {
                                        facing = Camera.Forward.X < 0 ? (byte)BlockFacing.East : (byte)BlockFacing.West;
                                    }

                                    world.SetBlockWorld(pb.X, pb.Y, pb.Z, isIron ? BlockType.IronDoorLower : BlockType.DoorLower, metadata: facing);
                                    world.SetBlockWorld(pb.X, pb.Y + 1, pb.Z, isIron ? BlockType.IronDoorUpper : BlockType.DoorUpper, metadata: facing);
                                    BlocksPlaced += 2;
                                    AudioManager?.PlayBlockPlaceSound(isIron ? BlockType.IronBlock : BlockType.Planks);
                                    Inventory.Slots[SelectedHotbarSlot].Count--;
                                    if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                                    {
                                        Inventory.Slots[SelectedHotbarSlot].Clear();
                                    }
                                }
                            }
                        }
                        // Special multi-block placement: Bed (2 blocks long)
                        else if (hotbarStack.Type == BlockType.BedFoot)
                        {
                            Vector3i forwardOffset;
                            byte facing; // 0 = South (+Z), 1 = North (-Z), 2 = West (-X), 3 = East (+X)

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

                            Vector3i headPos = pb + forwardOffset;
                            if (world.GetBlockWorld(headPos.X, headPos.Y, headPos.Z) == (byte)BlockType.Empty)
                            {
                                AABB footBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.1f, pb.Z + 0.5f);
                                AABB headBox = new AABB(headPos.X - 0.5f, headPos.Y - 0.5f, headPos.Z - 0.5f, headPos.X + 0.5f, headPos.Y + 0.1f, headPos.Z + 0.5f);
                                AABB pBox = GetAABB();

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
                                    {
                                        Inventory.Slots[SelectedHotbarSlot].Clear();
                                    }
                                }
                            }
                        }
                        else
                        {
                            // If block is solid, check intersection with player AABB
                            bool canPlace = true;
                            if (placeDef.IsSolid)
                            {
                                AABB blockBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.5f, pb.Z + 0.5f);
                                if (GetAABB().Intersects(blockBox))
                                {
                                    canPlace = false;
                                }
                            }

                            if (canPlace)
                            {
                                BlockType toPlace = hotbarStack.Type;

                                // Auto-activate powered rail if placed next to an active power source
                                if (toPlace == BlockType.PoweredRail && RedstoneSystem.HasActivePowerNeighbor(world, pb))
                                {
                                    toPlace = BlockType.PoweredRailOn;
                                }

                                if (toPlace == BlockType.WheatSeeds)
                                {
                                    byte under = world.GetBlockWorld(pb.X, pb.Y - 1, pb.Z);
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
                                    byte under = world.GetBlockWorld(pb.X, pb.Y - 1, pb.Z);
                                    if (under != (byte)BlockType.Grass && under != (byte)BlockType.Dirt && under != (byte)BlockType.SnowyGrass)
                                    {
                                        canPlace = false;
                                    }
                                    else
                                    {
                                        world.RegisterGrowingSapling(pb);
                                    }
                                }

                                if (!canPlace)
                                {
                                    return;
                                }

                                byte metadata = 0;
                                if (toPlace == BlockType.Chest)
                                {
                                    // Check if placing next to an existing chest (the first chest)
                                    Vector3i[] chestNeighbors = new Vector3i[]
                                    {
                                        new Vector3i(pb.X + 1, pb.Y, pb.Z),
                                        new Vector3i(pb.X - 1, pb.Y, pb.Z),
                                        new Vector3i(pb.X, pb.Y, pb.Z + 1),
                                        new Vector3i(pb.X, pb.Y, pb.Z - 1)
                                    };

                                    bool foundFirstChest = false;
                                    foreach (var n in chestNeighbors)
                                    {
                                        if (world.GetBlockWorld(n.X, n.Y, n.Z) == (byte)BlockType.Chest)
                                        {
                                            byte firstFacing = world.GetBlockMetadata(n.X, n.Y, n.Z);
                                            bool isAxisX = (pb.X != n.X);

                                            if (isAxisX)
                                            {
                                                // Placed along X: large chest faces South (0) or North (1)
                                                metadata = (firstFacing == (byte)BlockFacing.North)
                                                    ? (byte)BlockFacing.North
                                                    : (byte)BlockFacing.South;
                                            }
                                            else
                                            {
                                                // Placed along Z: large chest faces West (2) or East (3)
                                                metadata = (firstFacing == (byte)BlockFacing.East)
                                                    ? (byte)BlockFacing.East
                                                    : (byte)BlockFacing.West;
                                            }

                                            // Sync first chest's metadata to ensure both halves match the direction of the first chest!
                                            world.SetBlockMetadata(n.X, n.Y, n.Z, metadata);
                                            foundFirstChest = true;
                                            break;
                                        }
                                    }

                                    if (!foundFirstChest)
                                    {
                                        // Standalone chest faces towards player:
                                        if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                        {
                                            metadata = Camera.Forward.Z < 0 ? (byte)BlockFacing.South : (byte)BlockFacing.North;
                                        }
                                        else
                                        {
                                            metadata = Camera.Forward.X < 0 ? (byte)BlockFacing.East : (byte)BlockFacing.West;
                                        }
                                    }
                                }
                                else if (BlockRegistry.IsDirectionalBlock(toPlace))
                                {
                                    // Block faces towards player:
                                    if (MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X))
                                    {
                                        metadata = Camera.Forward.Z < 0 ? (byte)BlockFacing.South : (byte)BlockFacing.North;
                                    }
                                    else
                                    {
                                        metadata = Camera.Forward.X < 0 ? (byte)BlockFacing.East : (byte)BlockFacing.West;
                                    }
                                }
                                else if (BlockRegistry.IsRailBlock(toPlace))
                                {
                                    bool hasNorth = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X, pb.Y, pb.Z - 1));
                                    bool hasSouth = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X, pb.Y, pb.Z + 1));
                                    bool hasWest = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X - 1, pb.Y, pb.Z));
                                    bool hasEast = BlockRegistry.IsRailBlock(world.GetBlockWorld(pb.X + 1, pb.Y, pb.Z));

                                    if (toPlace == BlockType.Rail)
                                    {
                                        if (hasSouth && hasEast) metadata = (byte)RailShape.SouthEast;
                                        else if (hasSouth && hasWest) metadata = (byte)RailShape.SouthWest;
                                        else if (hasNorth && hasWest) metadata = (byte)RailShape.NorthWest;
                                        else if (hasNorth && hasEast) metadata = (byte)RailShape.NorthEast;
                                        else if (hasNorth || hasSouth) metadata = (byte)RailShape.NorthSouth;
                                        else if (hasEast || hasWest) metadata = (byte)RailShape.EastWest;
                                        else
                                        {
                                            metadata = MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X)
                                                ? (byte)RailShape.NorthSouth
                                                : (byte)RailShape.EastWest;
                                        }
                                    }
                                    else
                                    {
                                        if (hasNorth || hasSouth) metadata = (byte)RailShape.NorthSouth;
                                        else if (hasEast || hasWest) metadata = (byte)RailShape.EastWest;
                                        else
                                        {
                                            metadata = MathF.Abs(Camera.Forward.Z) > MathF.Abs(Camera.Forward.X)
                                                ? (byte)RailShape.NorthSouth
                                                : (byte)RailShape.EastWest;
                                        }
                                    }
                                }

                                world.SetBlockWorld(pb.X, pb.Y, pb.Z, toPlace, metadata);
                                if (toPlace == BlockType.Water || toPlace == BlockType.Lava)
                                {
                                    world.FluidSystem.TriggerFlow(world, pb, toPlace);
                                }
                                BlocksPlaced++;
                                AudioManager?.PlayBlockPlaceSound(toPlace);
                                Inventory.Slots[SelectedHotbarSlot].Count--;
                                if (Inventory.Slots[SelectedHotbarSlot].Count <= 0)
                                {
                                    Inventory.Slots[SelectedHotbarSlot].Clear();
                                }
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
            if (DamageFlashTimer > 0f)
            {
                DamageFlashTimer = MathF.Max(0f, DamageFlashTimer - deltaTime);
            }

            // 1. DROWNING CHECK (Head/eyes submerged in water)
            int eyeX = (int)Math.Floor(Position.X);
            int eyeY = (int)Math.Floor(Position.Y + EyeHeight);
            int eyeZ = (int)Math.Floor(Position.Z);
            byte eyeBlock = world.GetBlockWorld(eyeX, eyeY, eyeZ);
            bool isHeadInWater = (eyeBlock == (byte)BlockType.Water);

            if (isHeadInWater)
            {
                // Drain oxygen underwater: 100 to 0 in ~15 seconds (~6.67/s)
                Oxygen = MathF.Max(0f, Oxygen - 6.67f * deltaTime);

                if (Oxygen <= 0f)
                {
                    drownDamageTimer += deltaTime;
                    if (drownDamageTimer >= 1.0f)
                    {
                        drownDamageTimer = 0f;
                        TakeDamage(10.0f, "drown", world);
                        AudioManager?.PlayCategorySound("water", volume: 0.8f, pitchVariance: 0.15f);
                    }
                }
                else
                {
                    drownDamageTimer = 0f;
                }
            }
            else
            {
                // Surfaced: recover oxygen rapidly (full in ~2.5s)
                Oxygen = MathF.Min(MaxOxygen, Oxygen + 40.0f * deltaTime);
                drownDamageTimer = 0f;
            }

            // 2. LAVA CHECK (Feet, body, or head in lava)
            int footX = (int)Math.Floor(Position.X);
            int footY = (int)Math.Floor(Position.Y + 0.1f);
            int footZ = (int)Math.Floor(Position.Z);
            int bodyY = (int)Math.Floor(Position.Y + 0.9f);

            bool isInLava = (world.GetBlockWorld(footX, footY, footZ) == (byte)BlockType.Lava) ||
                           (world.GetBlockWorld(footX, bodyY, footZ) == (byte)BlockType.Lava) ||
                           (eyeBlock == (byte)BlockType.Lava);

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
                lavaDamageTimer = 0.45f; // Prime for quick reaction when stepping into lava
            }

            // 3. CACTUS CHECK (Touching or standing on cactus blocks)
            AABB pBox = GetAABB();
            float skin = 0.08f;
            AABB touchBox = new AABB(pBox.Min.X - skin, pBox.Min.Y - skin, pBox.Min.Z - skin,
                                     pBox.Max.X + skin, pBox.Max.Y + skin, pBox.Max.Z + skin);

            int minCX = (int)MathF.Floor(touchBox.Min.X - 0.5f);
            int maxCX = (int)MathF.Ceiling(touchBox.Max.X + 0.5f);
            int minCY = (int)MathF.Floor(touchBox.Min.Y - 0.5f);
            int maxCY = (int)MathF.Ceiling(touchBox.Max.Y + 0.5f);
            int minCZ = (int)MathF.Floor(touchBox.Min.Z - 0.5f);
            int maxCZ = (int)MathF.Ceiling(touchBox.Max.Z + 0.5f);

            bool isTouchingCactus = false;
            for (int x = minCX; x <= maxCX; x++)
            {
                for (int y = minCY; y <= maxCY; y++)
                {
                    for (int z = minCZ; z <= maxCZ; z++)
                    {
                        if (world.GetBlockWorld(x, y, z) == (byte)BlockType.Cactus)
                        {
                            AABB cactusBox = new AABB(x - 0.5f, y - 0.5f, z - 0.5f, x + 0.5f, y + 0.5f, z + 0.5f);
                            if (touchBox.Intersects(cactusBox))
                            {
                                isTouchingCactus = true;
                                goto DoneCactusCheck;
                            }
                        }
                    }
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
                cactusDamageTimer = 0.45f; // Prime for quick reaction when touching cactus
            }

            // 4. PASSIVE HEALING
            // Only heals after 10 seconds of receiving damage
            if (Health < MaxHealth && Health > 0f && timeSinceLastDamage >= 10.0f && !isInLava && !isHeadInWater && !isTouchingCactus)
            {
                Health = MathF.Min(MaxHealth, Health + 6.0f * deltaTime);
            }
        }

        public void TakeDamage(float amount, string damageType, World.World world)
        {
            if (amount <= 0f || Health <= 0f) return;

            Health = MathF.Max(0f, Health - amount);
            timeSinceLastDamage = 0f; // Resets the 10-second timer on any damage taken
            DamageFlashTimer = 0.35f;

            AudioManager?.PlayPlayerHurt();

            if (Health <= 0f)
            {
                DieAndRespawn(world);
            }
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

            if (IsRiding)
            {
                DismountMinecart();
            }

            // Respawn player safely at world spawn location
            int spawnX = (int)Math.Floor(SpawnPosition.X);
            int spawnZ = (int)Math.Floor(SpawnPosition.Z);
            int spawnY = world.GetSurfaceHeight(spawnX, spawnZ);
            Position = new Vector3(spawnX + 0.5f, spawnY + 2.5f, spawnZ + 0.5f);
            UpdateCamera();
        }
    }
}
