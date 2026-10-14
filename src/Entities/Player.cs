using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelRenderEngine.src.Physics;
using VoxelRenderEngine.src.World;

namespace VoxelRenderEngine.src.Entities
{
    public class Player
    {
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; } = Vector3.Zero;

        // Player physical bounding box: 0.6 wide x 1.8 high
        public float Width { get; } = 0.6f;
        public float Height { get; } = 1.8f;
        public float EyeHeight { get; } = 1.62f;

        public bool IsGrounded { get; private set; } = false;
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
        public Audio.AudioManager? AudioManager { get; set; }
        public Action? OnPortalEntered { get; set; }

        public int BlocksBroken { get; set; } = 0;
        public int BlocksPlaced { get; set; } = 0;
        private float footstepTimer = 0f;

        public readonly List<ItemDrop> DroppedItems = new List<ItemDrop>();

        public Player(Vector3 startPosition, Camera camera)
        {
            Position = startPosition;
            Camera = camera;
            UpdateCamera();

            // Starter items for testing crafting, smelting, placing, chest storage
            Inventory.Slots[0] = new UI.ItemStack(BlockType.Log, 16);
            Inventory.Slots[1] = new UI.ItemStack(BlockType.Cobblestone, 32);
            Inventory.Slots[2] = new UI.ItemStack(BlockType.IronOre, 8);
            Inventory.Slots[3] = new UI.ItemStack(BlockType.Coal, 16);
            Inventory.Slots[4] = new UI.ItemStack(BlockType.Sand, 16);
            Inventory.Slots[5] = new UI.ItemStack(BlockType.Chest, 4);
            Inventory.Slots[6] = new UI.ItemStack(BlockType.Dirt, 64);
            Inventory.Slots[7] = new UI.ItemStack(BlockType.CraftingTable, 1);
            Inventory.Slots[8] = new UI.ItemStack(BlockType.Furnace, 1);

            // Phase 5 & 6 interactive and endgame items
            Inventory.Slots[9] = new UI.ItemStack(BlockType.Torch, 16);
            Inventory.Slots[10] = new UI.ItemStack(BlockType.DoorLower, 4);
            Inventory.Slots[11] = new UI.ItemStack(BlockType.Lever, 4);
            Inventory.Slots[12] = new UI.ItemStack(BlockType.StoneButton, 4);
            Inventory.Slots[13] = new UI.ItemStack(BlockType.Rail, 32);
            Inventory.Slots[14] = new UI.ItemStack(BlockType.PoweredRail, 16);
            Inventory.Slots[15] = new UI.ItemStack(BlockType.MinecartItem, 2);
            Inventory.Slots[16] = new UI.ItemStack(BlockType.Obsidian, 14);
            Inventory.Slots[17] = new UI.ItemStack(BlockType.FlintAndSteel, 1);
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

                    UpdateCamera();
                    UpdateRaycast(world);
                    HandleBlockInteractions(world, mouse, deltaTime);
                    RedstoneSystem.Update(world, deltaTime);
                    UpdateItemDrops(world, deltaTime);
                    return;
                }
            }

            HandleFlyToggle(keyboard);

            if (IsFlying)
            {
                UpdateFlyMovement(keyboard, deltaTime);
            }
            else
            {
                UpdateWalkMovement(world, keyboard, deltaTime);
            }

            UpdateCamera();
            UpdateRaycast(world);
            HandleBlockInteractions(world, mouse, deltaTime);
            RedstoneSystem.Update(world, deltaTime);
            UpdateItemDrops(world, deltaTime);

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
            }
            fKeyWasDown = keyboard.IsKeyDown(Keys.F);
        }

        private void UpdateFlyMovement(KeyboardState keyboard, float deltaTime)
        {
            float speed = MoveSpeed * 3.5f;
            if (keyboard.IsKeyDown(Keys.LeftControl)) speed *= 2.0f;

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
        }

        private void UpdateWalkMovement(World.World world, KeyboardState keyboard, float deltaTime)
        {
            float speed = MoveSpeed;
            if (keyboard.IsKeyDown(Keys.LeftControl)) speed *= SprintMultiplier;

            // Check if player is standing/swimming in water or lava
            int footX = (int)Math.Floor(Position.X);
            int footY = (int)Math.Floor(Position.Y + 0.2f);
            int footZ = (int)Math.Floor(Position.Z);
            byte footBlock = world.GetBlockWorld(footX, footY, footZ);
            bool inLiquid = (footBlock == (byte)BlockType.Water || footBlock == (byte)BlockType.Lava);

            if (inLiquid)
            {
                speed *= 0.65f;
            }

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
                    vy = 3.8f; // Swim upwards
                }
                else if (keyboard.IsKeyDown(Keys.LeftShift))
                {
                    vy = -3.8f; // Dive downwards
                }
            }
            else
            {
                bool wasGrounded = IsGrounded;
                float prevVelY = Velocity.Y;

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

                // Landing sound
                if (!wasGrounded && IsGrounded && prevVelY < -3.8f)
                {
                    AudioManager?.PlayPlayerLand();
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
                        AudioManager?.PlayBlockStepSound((BlockType)groundBlock);
                    }
                }
                else
                {
                    footstepTimer = 0.25f;
                }
                return;
            }

            Velocity = new Vector3(vx, vy, vz);

            // Move and collide against voxel terrain
            MoveWithCollision(world, Velocity * deltaTime);
            ClampToWorldBounds();
        }

        private void MoveWithCollision(World.World world, Vector3 movement)
        {
            AABB playerBox = GetAABB();

            // Broad-phase query surrounding blocks
            int minX = (int)MathF.Floor(playerBox.Min.X + MathF.Min(0, movement.X) - 0.5f);
            int maxX = (int)MathF.Ceiling(playerBox.Max.X + MathF.Max(0, movement.X) + 0.5f);
            int minY = (int)MathF.Floor(playerBox.Min.Y + MathF.Min(0, movement.Y) - 0.5f);
            int maxY = (int)MathF.Ceiling(playerBox.Max.Y + MathF.Max(0, movement.Y) + 0.5f);
            int minZ = (int)MathF.Floor(playerBox.Min.Z + MathF.Min(0, movement.Z) - 0.5f);
            int maxZ = (int)MathF.Ceiling(playerBox.Max.Z + MathF.Max(0, movement.Z) + 0.5f);

            var candidateBoxes = new List<AABB>();
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
                        else if (movement.Y < 0 && playerBox.Min.Y >= box.Max.Y && playerBox.Min.Y + movement.Y < box.Max.Y)
                        {
                            movement.Y = box.Max.Y - playerBox.Min.Y;
                            Velocity = new Vector3(Velocity.X, 0, Velocity.Z);
                            IsGrounded = true;
                        }
                    }
                }
                Position = new Vector3(Position.X, Position.Y + movement.Y, Position.Z);
                playerBox = GetAABB();
            }

            // Check if still grounded
            if (movement.Y < -0.01f)
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

            foreach (var cart in world.Minecarts)
            {
                if (!cart.IsAlive || cart == CurrentMinecart) continue;

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

        private bool rightClickWasDown = false;
        private void HandleBlockInteractions(World.World world, MouseState mouse, float deltaTime)
        {
            // 0. Interaction with Minecart Entities
            if (TargetedMinecart != null)
            {
                // Left Click -> Break minecart (drop item)
                if (mouse.IsButtonDown(MouseButton.Left) && !IsRiding)
                {
                    TargetedMinecart.IsAlive = false;
                    world.Minecarts.Remove(TargetedMinecart);
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
                        BlocksBroken++;
                        AudioManager?.PlayBlockBreakSound(def.Type);

                        BlockType dropType = def.Type;

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

                        DroppedItems.Add(new ItemDrop(dropType, new Vector3(bp.X, bp.Y, bp.Z)));

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
                        world.Minecarts.Add(cart);
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

                    // A. Chest interaction
                    if (blockId == (byte)BlockType.Chest)
                    {
                        if (ChestManager != null && OnOpenChest != null)
                        {
                            var chestInv = ChestManager.GetOrCreateChest(bp);
                            OnOpenChest.Invoke(bp, chestInv);
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
                }

                // Normal block placement from hotbar
                if (PlacementBlock.HasValue)
                {
                    var pb = PlacementBlock.Value;
                    hotbarStack = Inventory.Slots[SelectedHotbarSlot];

                    if (!hotbarStack.IsEmpty && hotbarStack.Type != BlockType.Empty)
                    {
                        var placeDef = BlockRegistry.Get(hotbarStack.Type);

                        // Special multi-block placement: Wooden Door (2 blocks tall)
                        if (hotbarStack.Type == BlockType.DoorLower)
                        {
                            if (pb.Y + 1 < Chunk.SIZE_Y && world.GetBlockWorld(pb.X, pb.Y + 1, pb.Z) == (byte)BlockType.Empty)
                            {
                                AABB lowerBox = new AABB(pb.X - 0.5f, pb.Y - 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 0.5f, pb.Z + 0.5f);
                                AABB upperBox = new AABB(pb.X - 0.5f, pb.Y + 0.5f, pb.Z - 0.5f, pb.X + 0.5f, pb.Y + 1.5f, pb.Z + 0.5f);
                                AABB pBox = GetAABB();

                                if (!pBox.Intersects(lowerBox) && !pBox.Intersects(upperBox))
                                {
                                    world.SetBlockWorld(pb.X, pb.Y, pb.Z, BlockType.DoorLower);
                                    world.SetBlockWorld(pb.X, pb.Y + 1, pb.Z, BlockType.DoorUpper);
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

                                world.SetBlockWorld(pb.X, pb.Y, pb.Z, toPlace);
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
    }
}
