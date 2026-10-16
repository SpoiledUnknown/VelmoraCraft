using System.Text.Json;
using OpenTK.Mathematics;
using VoxelRenderEngine.src.Entities;
using VoxelRenderEngine.src.UI;

namespace VoxelRenderEngine.src.World
{
    public class SaveData
    {
        public int Version { get; set; } = 1;
        public int Seed { get; set; } = 1337;
        public int WorldSize { get; set; } = 64;
        public int WorldChunks => WorldSize;
        public float TimeOfDay { get; set; } = 150.0f;

        // Player state
        public float PlayerPosX { get; set; }
        public float PlayerPosY { get; set; }
        public float PlayerPosZ { get; set; }
        public float PlayerX => PlayerPosX;
        public float PlayerY => PlayerPosY;
        public float PlayerZ => PlayerPosZ;
        public float PlayerYaw { get; set; }
        public float PlayerPitch { get; set; }
        public bool PlayerIsFlying { get; set; }
        public int SelectedHotbarSlot { get; set; }
        public float PlayerHealth { get; set; } = 100.0f;
        public float PlayerOxygen { get; set; } = 100.0f;

        // Statistics
        public int BlocksBroken { get; set; }
        public int BlocksPlaced { get; set; }
        public float TotalPlayTime { get; set; }
        public float PlayTimeSeconds => TotalPlayTime;

        // Player Inventory
        public List<SlotData> InventorySlots { get; set; } = new List<SlotData>();

        // Modified Blocks (delta from procedural generator)
        public List<BlockChangeData> ModifiedBlocks { get; set; } = new List<BlockChangeData>();

        // Chest Inventories
        public List<ChestSaveData> Chests { get; set; } = new List<ChestSaveData>();
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
    }

    public class ChestSaveData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public List<SlotData> Items { get; set; } = new List<SlotData>();
    }

    public static class SaveSystem
    {
        private static string SaveDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saves");
        private static string SaveFilePath => Path.Combine(SaveDirectory, "world.json");

        public static bool HasSave()
        {
            return File.Exists(SaveFilePath);
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Error deleting save: {ex.Message}");
            }
        }

        public static bool SaveWorld(World world, Player player, DayNightCycle? dayNight, ChestManager? chestManager, float totalPlayTime = 0f, int blocksBroken = 0, int blocksPlaced = 0)
        {
            try
            {
                if (!Directory.Exists(SaveDirectory))
                {
                    Directory.CreateDirectory(SaveDirectory);
                }

                var data = new SaveData
                {
                    Seed = world.Seed,
                    WorldSize = world.WorldChunksX,
                    TimeOfDay = dayNight?.TimeOfDay ?? 150.0f,
                    PlayerPosX = player.Position.X,
                    PlayerPosY = player.Position.Y,
                    PlayerPosZ = player.Position.Z,
                    PlayerYaw = player.Camera.Yaw,
                    PlayerPitch = player.Camera.Pitch,
                    PlayerIsFlying = player.IsFlying,
                    SelectedHotbarSlot = player.SelectedHotbarSlot,
                    PlayerHealth = player.Health,
                    PlayerOxygen = player.Oxygen,
                    TotalPlayTime = totalPlayTime,
                    BlocksBroken = blocksBroken,
                    BlocksPlaced = blocksPlaced
                };

                // Save Player Inventory
                for (int i = 0; i < player.Inventory.SlotCount; i++)
                {
                    var slot = player.Inventory.Slots[i];
                    if (!slot.IsEmpty)
                    {
                        data.InventorySlots.Add(new SlotData
                        {
                            Index = i,
                            BlockType = (byte)slot.Type,
                            Count = slot.Count
                        });
                    }
                }

                // Save Modified Blocks
                foreach (var kvp in world.ModifiedBlocks)
                {
                    data.ModifiedBlocks.Add(new BlockChangeData
                    {
                        X = kvp.Key.X,
                        Y = kvp.Key.Y,
                        Z = kvp.Key.Z,
                        BlockType = (byte)kvp.Value
                    });
                }

                // Save Chests
                if (chestManager != null)
                {
                    foreach (var kvp in chestManager.AllChests)
                    {
                        var chestData = new ChestSaveData
                        {
                            X = kvp.Key.X,
                            Y = kvp.Key.Y,
                            Z = kvp.Key.Z
                        };

                        var chestInv = kvp.Value;
                        for (int i = 0; i < chestInv.SlotCount; i++)
                        {
                            if (!chestInv.Slots[i].IsEmpty)
                            {
                                chestData.Items.Add(new SlotData
                                {
                                    Index = i,
                                    BlockType = (byte)chestInv.Slots[i].Type,
                                    Count = chestInv.Slots[i].Count
                                });
                            }
                        }

                        data.Chests.Add(chestData);
                    }
                }

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SaveFilePath, json);
                Console.WriteLine($"[SaveSystem] World saved successfully ({data.ModifiedBlocks.Count} block changes, {data.Chests.Count} chests).");
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

                string json = File.ReadAllText(SaveFilePath);
                return JsonSerializer.Deserialize<SaveData>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSystem] Load failed: {ex.Message}");
                return null;
            }
        }

        public static void ApplyLoadedData(SaveData data, World world, Player player, DayNightCycle? dayNight, ChestManager? chestManager)
        {
            // Restore Day/Night cycle
            if (dayNight != null)
            {
                dayNight.TimeOfDay = data.TimeOfDay;
            }

            // Restore Player position & rotation
            player.Position = new Vector3(data.PlayerPosX, data.PlayerPosY, data.PlayerPosZ);
            player.Camera.Position = player.Position + new Vector3(0, player.EyeHeight, 0);
            player.Camera.Yaw = data.PlayerYaw;
            player.Camera.Pitch = data.PlayerPitch;
            player.Camera.UpdateVectors();
            player.IsFlying = data.PlayerIsFlying;
            player.SelectedHotbarSlot = Math.Clamp(data.SelectedHotbarSlot, 0, 8);
            player.Health = (data.PlayerHealth > 0f) ? data.PlayerHealth : player.MaxHealth;
            player.Oxygen = (data.PlayerOxygen > 0f) ? data.PlayerOxygen : player.MaxOxygen;
            player.Velocity = Vector3.Zero;

            // Restore Inventory
            player.Inventory.Clear();
            foreach (var slot in data.InventorySlots)
            {
                if (slot.Index >= 0 && slot.Index < player.Inventory.SlotCount)
                {
                    player.Inventory.Slots[slot.Index] = new ItemStack((BlockType)slot.BlockType, slot.Count);
                }
            }

            // Restore Modified Blocks
            foreach (var b in data.ModifiedBlocks)
            {
                world.SetBlockWorld(b.X, b.Y, b.Z, (BlockType)b.BlockType);
            }

            // Restore Chests
            if (chestManager != null)
            {
                chestManager.Clear();
                foreach (var c in data.Chests)
                {
                    var pos = new Vector3i(c.X, c.Y, c.Z);
                    var inv = chestManager.GetOrCreateChest(pos);
                    foreach (var it in c.Items)
                    {
                        if (it.Index >= 0 && it.Index < inv.SlotCount)
                        {
                            inv.Slots[it.Index] = new ItemStack((BlockType)it.BlockType, it.Count);
                        }
                    }
                }
            }
        }
    }
}
