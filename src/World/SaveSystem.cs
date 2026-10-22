using System.Text.Json;
using OpenTK.Mathematics;
using VelmoraCraft.src.Entities;
using VelmoraCraft.src.UI;

namespace VelmoraCraft.src.World;

public class SaveData
{
    public int Version { get; set; } = 1;
    public int Seed { get; set; } = 1337;
    public int WorldSize { get; set; } = 64;
    public int WorldChunks => WorldSize;
    public float TimeOfDay { get; set; } = 150.0f;

    public float PlayerPosX { get; set; }
    public float PlayerPosY { get; set; }
    public float PlayerPosZ { get; set; }
    public float PlayerX => PlayerPosX;
    public float PlayerY => PlayerPosY;
    public float PlayerZ => PlayerPosZ;
    public float PlayerYaw { get; set; }
    public float PlayerPitch { get; set; }
    public bool PlayerIsFlying { get; set; }
    public bool IsFullscreen { get; set; }
    public bool IsHudEnabled { get; set; } = true;
    public int RenderDistanceChunks { get; set; } = 8;
    public float FieldOfView { get; set; } = 70.0f;
    public int SelectedHotbarSlot { get; set; }
    public float PlayerHealth { get; set; } = 100.0f;
    public float PlayerOxygen { get; set; } = 100.0f;

    public int BlocksBroken { get; set; }
    public int BlocksPlaced { get; set; }
    public float TotalPlayTime { get; set; }
    public float PlayTimeSeconds => TotalPlayTime;

    public List<SlotData> InventorySlots { get; set; } = new();
    public List<BlockChangeData> ModifiedBlocks { get; set; } = new();
    public List<ChestSaveData> Chests { get; set; } = new();
    public List<DirtSpreadSaveData> PendingDirtSpreads { get; set; } = new();
    public List<DirtSpreadSaveData> GrowingSaplings { get; set; } = new();
    public List<DirtSpreadSaveData> PendingDecayingLeaves { get; set; } = new();
}

public class DirtSpreadSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public float RemainingSeconds { get; set; }
}

public class SlotData
{
    public int Index { get; set; }
    public byte BlockType { get; set; }
    public int Count { get; set; }
}

public class BlockChangeData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public byte BlockType { get; set; }
    public byte Metadata { get; set; }
}

public class ChestSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public List<SlotData> Items { get; set; } = new();
}

public class GameSettings
{
    public bool IsFlying { get; set; }
    public bool IsFullscreen { get; set; }
    public bool IsHudEnabled { get; set; } = true;
    public int RenderDistanceChunks { get; set; } = 8;
    public float FieldOfView { get; set; } = 70.0f;
}

public static class SaveSystem
{
    private static string SaveDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves");
    private static string SaveFilePath => Path.Combine(SaveDirectory, "world.json");
    private static string SettingsFilePath => Path.Combine(SaveDirectory, "options.json");

    public static void SaveSettings(GameSettings settings)
    {
        try
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSystem] Failed to save options: {ex.Message}");
        }
    }

    public static GameSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<GameSettings>(json) ?? new GameSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSystem] Failed to load options: {ex.Message}");
        }

        return new GameSettings();
    }

    public static bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSystem] Error deleting save: {ex.Message}");
        }
    }

    public static bool SaveWorld(World world, Player player, DayNightCycle? dayNight, ChestManager? chestManager,
        float totalPlayTime = 0f, int blocksBroken = 0, int blocksPlaced = 0, UIManager? uiManager = null,
        bool isFullscreen = false)
    {
        try
        {
            if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);

            var renderDist = uiManager?.RenderDistanceChunks ?? world.ViewDistanceChunks;
            var fov = player.Camera?.BaseFov ?? uiManager?.FieldOfView ?? 70.0f;
            var hud = uiManager?.IsHudEnabled ?? true;

            var data = new SaveData
            {
                Seed = world.Seed,
                WorldSize = world.WorldChunksX,
                TimeOfDay = dayNight?.TimeOfDay ?? 150.0f,
                PlayerPosX = player.Position.X,
                PlayerPosY = player.Position.Y,
                PlayerPosZ = player.Position.Z,
                PlayerYaw = player.Camera?.Yaw ?? -90f,
                PlayerPitch = player.Camera?.Pitch ?? 0f,
                PlayerIsFlying = player.IsFlying,
                IsFullscreen = isFullscreen,
                IsHudEnabled = hud,
                RenderDistanceChunks = renderDist,
                FieldOfView = fov,
                SelectedHotbarSlot = player.SelectedHotbarSlot,
                PlayerHealth = player.Health,
                PlayerOxygen = player.Oxygen,
                TotalPlayTime = totalPlayTime,
                BlocksBroken = blocksBroken,
                BlocksPlaced = blocksPlaced
            };

            SaveSettings(new GameSettings
            {
                IsFlying = player.IsFlying,
                IsFullscreen = isFullscreen,
                IsHudEnabled = hud,
                RenderDistanceChunks = renderDist,
                FieldOfView = fov
            });

            for (var i = 0; i < player.Inventory.SlotCount; i++)
            {
                var slot = player.Inventory.Slots[i];
                if (!slot.IsEmpty)
                    data.InventorySlots.Add(new SlotData
                    {
                        Index = i,
                        BlockType = (byte)slot.Type,
                        Count = slot.Count
                    });
            }

            foreach (var kvp in world.ModifiedBlocks)
                data.ModifiedBlocks.Add(new BlockChangeData
                {
                    X = kvp.Key.X,
                    Y = kvp.Key.Y,
                    Z = kvp.Key.Z,
                    BlockType = (byte)kvp.Value,
                    Metadata = world.GetBlockMetadata(kvp.Key.X, kvp.Key.Y, kvp.Key.Z)
                });

            if (chestManager != null)
                foreach (var kvp in chestManager.AllChests)
                {
                    var chestData = new ChestSaveData
                    {
                        X = kvp.Key.X,
                        Y = kvp.Key.Y,
                        Z = kvp.Key.Z
                    };

                    var chestInv = kvp.Value;
                    for (var i = 0; i < chestInv.SlotCount; i++)
                        if (!chestInv.Slots[i].IsEmpty)
                            chestData.Items.Add(new SlotData
                            {
                                Index = i,
                                BlockType = (byte)chestInv.Slots[i].Type,
                                Count = chestInv.Slots[i].Count
                            });

                    data.Chests.Add(chestData);
                }

            foreach (var kvp in world.PendingDirtGrassSpread)
                data.PendingDirtSpreads.Add(new DirtSpreadSaveData
                {
                    X = kvp.Key.X,
                    Y = kvp.Key.Y,
                    Z = kvp.Key.Z,
                    RemainingSeconds = kvp.Value
                });

            foreach (var kvp in world.GrowingSaplings)
                data.GrowingSaplings.Add(new DirtSpreadSaveData
                {
                    X = kvp.Key.X,
                    Y = kvp.Key.Y,
                    Z = kvp.Key.Z,
                    RemainingSeconds = kvp.Value
                });

            foreach (var kvp in world.PendingDecayingLeaves)
                data.PendingDecayingLeaves.Add(new DirtSpreadSaveData
                {
                    X = kvp.Key.X,
                    Y = kvp.Key.Y,
                    Z = kvp.Key.Z,
                    RemainingSeconds = kvp.Value
                });

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SaveFilePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSystem] Save failed: {ex.Message}");
            return false;
        }
    }

    public static SaveData? LoadSaveData()
    {
        try
        {
            if (!HasSave()) return null;

            var json = File.ReadAllText(SaveFilePath);
            return JsonSerializer.Deserialize<SaveData>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SaveSystem] Load failed: {ex.Message}");
            return null;
        }
    }

    public static void ApplyLoadedData(SaveData data, World world, Player player, DayNightCycle? dayNight,
        ChestManager? chestManager, UIManager? uiManager = null, Action<bool>? setFullscreen = null)
    {
        if (dayNight != null) dayNight.TimeOfDay = data.TimeOfDay;

        var modifiedChunks = new HashSet<Vector2i>();
        foreach (var b in data.ModifiedBlocks)
        {
            world.SetBlockWorld(b.X, b.Y, b.Z, (BlockType)b.BlockType, b.Metadata);
            var cx = (int)Math.Floor((float)b.X / Chunk.SIZE_X);
            var cz = (int)Math.Floor((float)b.Z / Chunk.SIZE_Z);
            modifiedChunks.Add(new Vector2i(cx, cz));
        }

        foreach (var mc in modifiedChunks) world.EnqueueChunk(mc);

        world.PendingDirtGrassSpread.Clear();
        if (data.PendingDirtSpreads != null)
            foreach (var ds in data.PendingDirtSpreads)
                world.PendingDirtGrassSpread[new Vector3i(ds.X, ds.Y, ds.Z)] = ds.RemainingSeconds;

        foreach (var b in data.ModifiedBlocks)
            if (b.BlockType == (byte)BlockType.Dirt)
            {
                var pos = new Vector3i(b.X, b.Y, b.Z);
                if (!world.PendingDirtGrassSpread.ContainsKey(pos)) world.CheckAndRegisterDirtForGrassSpread(pos);
            }

        world.GrowingSaplings.Clear();
        if (data.GrowingSaplings != null)
            foreach (var gs in data.GrowingSaplings)
                world.GrowingSaplings[new Vector3i(gs.X, gs.Y, gs.Z)] = gs.RemainingSeconds;

        world.PendingDecayingLeaves.Clear();
        if (data.PendingDecayingLeaves != null)
            foreach (var dl in data.PendingDecayingLeaves)
                world.PendingDecayingLeaves[new Vector3i(dl.X, dl.Y, dl.Z)] = dl.RemainingSeconds;

        if (chestManager != null)
        {
            chestManager.Clear();
            foreach (var c in data.Chests)
            {
                var pos = new Vector3i(c.X, c.Y, c.Z);
                var inv = chestManager.GetOrCreateChest(pos);
                foreach (var it in c.Items)
                    if (it.Index >= 0 && it.Index < inv.SlotCount)
                        inv.Slots[it.Index] = new ItemStack((BlockType)it.BlockType, it.Count);
            }
        }

        player.Inventory.Clear();
        foreach (var slot in data.InventorySlots)
            if (slot.Index >= 0 && slot.Index < player.Inventory.SlotCount)
                player.Inventory.Slots[slot.Index] = new ItemStack((BlockType)slot.BlockType, slot.Count);

        var playerX = data.PlayerPosX;
        var playerZ = data.PlayerPosZ;
        var targetY = data.PlayerPosY;

        if (!data.PlayerIsFlying)
        {
            var highestSolidTop = float.MinValue;
            var halfW = player.Width * 0.5f;

            float[] sampleOffsetsX = { -halfW + 0.05f, 0f, halfW - 0.05f };
            float[] sampleOffsetsZ = { -halfW + 0.05f, 0f, halfW - 0.05f };

            var searchStartY = (int)MathF.Floor(data.PlayerPosY + 1.0f);
            searchStartY = Math.Clamp(searchStartY, 1, Chunk.SIZE_Y - 2);
            var searchEndY = Math.Max(1, (int)MathF.Floor(data.PlayerPosY - 8.0f));

            for (var y = searchStartY; y >= searchEndY; y--)
            {
                var foundSolidAtY = false;
                foreach (var ox in sampleOffsetsX)
                foreach (var oz in sampleOffsetsZ)
                {
                    var bx = (int)MathF.Round(playerX + ox);
                    var bz = (int)MathF.Round(playerZ + oz);
                    var b = world.GetBlockWorld(bx, y, bz);
                    if (BlockRegistry.IsSolid(b))
                    {
                        var blockTop = y + 0.5f;
                        if (blockTop > highestSolidTop) highestSolidTop = blockTop;
                        foundSolidAtY = true;
                    }
                }

                if (foundSolidAtY && highestSolidTop <= data.PlayerPosY + 0.85f) break;
            }

            if (highestSolidTop > float.MinValue) targetY = MathF.Max(data.PlayerPosY, highestSolidTop);
        }

        player.Position = new Vector3(playerX, targetY, playerZ);
        player.Velocity = Vector3.Zero;
        player.FallDistance = 0f;
        player.IsFlying = data.PlayerIsFlying;
        player.IsGrounded = !data.PlayerIsFlying;

        player.ResolveStuckInBlocks(world);

        player.Camera.Position = player.Position + new Vector3(0, player.EyeHeight, 0);
        player.Camera.Yaw = data.PlayerYaw;
        player.Camera.Pitch = data.PlayerPitch;
        player.Camera.UpdateVectors();
        player.SelectedHotbarSlot = Math.Clamp(data.SelectedHotbarSlot, 0, 8);
        player.Health = data.PlayerHealth > 0f ? data.PlayerHealth : player.MaxHealth;
        player.Oxygen = data.PlayerOxygen > 0f ? data.PlayerOxygen : player.MaxOxygen;

        if (uiManager != null)
        {
            uiManager.IsHudEnabled = data.IsHudEnabled;
            uiManager.SetRenderDistance(data.RenderDistanceChunks > 0 ? data.RenderDistanceChunks : 8);
            uiManager.FieldOfView = data.FieldOfView >= 30f && data.FieldOfView <= 110f ? data.FieldOfView : 70.0f;
        }

        if (player.Camera != null)
        {
            player.Camera.BaseFov = data.FieldOfView >= 30f && data.FieldOfView <= 110f ? data.FieldOfView : 70.0f;
            player.Camera.CurrentFov = player.Camera.BaseFov;
        }

        world.ViewDistanceChunks = data.RenderDistanceChunks > 0 ? data.RenderDistanceChunks : 8;
        setFullscreen?.Invoke(data.IsFullscreen);
    }
}