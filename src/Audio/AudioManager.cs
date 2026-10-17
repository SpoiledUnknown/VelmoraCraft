using System.Runtime.InteropServices;
using OpenTK.Audio.OpenAL;
using OpenTK.Audio.OpenAL.ALC;
using OpenTK.Mathematics;
using VoxelRenderEngine.src.World;
using ALC = OpenTK.Audio.OpenAL.ALC.ALC;
using AL = OpenTK.Audio.OpenAL.AL;

namespace VoxelRenderEngine.src.Audio
{
    public class AudioManager : IDisposable
    {
        private ALCDevice device;
        private ALCContext context;
        private bool isInitialized = false;

        // Sound effect cache: path -> OpenAL buffer ID
        private readonly Dictionary<string, int> bufferCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Sound effect source pool
        private const int SOURCE_POOL_SIZE = 16;
        private readonly int[] sfxSources = new int[SOURCE_POOL_SIZE];
        private int nextSourceIndex = 0;

        // Dedicated music source
        private int musicSource = 0;
        private string? currentPlayingMusic = null;

        // Configurable music paths
        public string? AmbientMusicPath { get; set; }
        public string? DayMusicPath { get; set; }
        public string? NightMusicPath { get; set; }

        public float SoundVolume { get; set; } = 0.8f;
        public float MusicVolume { get; set; } = 0.5f;

        private DayNightPhase lastObservedPhase = DayNightPhase.Day;
        private readonly Random random = new Random();

        // Cached list of sounds per category for instant playback
        private readonly Dictionary<string, string[]> soundCategoryFiles = new Dictionary<string, string[]>();

        public AudioManager()
        {
            InitializeOpenAL();
            DiscoverAudioFiles();

            if (!string.IsNullOrEmpty(AmbientMusicPath))
            {
                PlayMusic(AmbientMusicPath, loop: true);
            }
        }

        private unsafe void InitializeOpenAL()
        {
            try
            {
                // Preload openal32.dll from the application base directory if present
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localOpenAl = Path.Combine(baseDir, "openal32.dll");
                if (File.Exists(localOpenAl))
                {
                    NativeLibrary.TryLoad(localOpenAl, out _);
                }
                else
                {
                    NativeLibrary.TryLoad("openal32.dll", typeof(AudioManager).Assembly, null, out _);
                }

                device = ALC.OpenDevice((byte*)null);
                if (device.Handle == IntPtr.Zero)
                {
                    Console.WriteLine("[Audio] Warning: Failed to open default audio device. Running in silent mode.");
                    return;
                }

                context = ALC.CreateContext(device, (int*)null);
                if (context.Handle == IntPtr.Zero)
                {
                    Console.WriteLine("[Audio] Warning: Failed to create OpenAL context.");
                    return;
                }

                ALC.MakeContextCurrent(context);

                // Create source pool for SFX
                for (int i = 0; i < SOURCE_POOL_SIZE; i++)
                {
                    sfxSources[i] = AL.GenSource();
                }

                // Create music source
                musicSource = AL.GenSource();
                AL.Sourcei(musicSource, SourcePNameI.Looping, 1);

                isInitialized = true;
                Console.WriteLine("[Audio] OpenAL Sound System initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] OpenAL initialization failed: {ex.Message}. Running without audio.");
                isInitialized = false;
            }
        }

        private void DiscoverAudioFiles()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string audioDir = Path.Combine(baseDir, "Audio");
            if (!Directory.Exists(audioDir))
            {
                audioDir = "Audio";
            }

            if (Directory.Exists(audioDir))
            {
                foreach (var dir in Directory.GetDirectories(audioDir))
                {
                    string category = Path.GetFileName(dir).ToLowerInvariant();
                    var soundFiles = Directory.GetFiles(dir, "*.*")
                        .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (soundFiles.Length > 0)
                    {
                        soundCategoryFiles[category] = soundFiles;
                    }
                }

                // Check for bg1 or ambient music in Audio directory root
                var rootAudioFiles = Directory.GetFiles(audioDir, "*.*")
                    .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                var bg1Candidate = rootAudioFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("bg1", StringComparison.OrdinalIgnoreCase));
                if (bg1Candidate != null)
                {
                    AmbientMusicPath = bg1Candidate;
                }
            }

            // Fallback check in current working directory / Audio if running via dotnet run
            if (string.IsNullOrEmpty(AmbientMusicPath))
            {
                string cwdAudio = Path.Combine(Directory.GetCurrentDirectory(), "Audio");
                if (Directory.Exists(cwdAudio))
                {
                    var cwdFiles = Directory.GetFiles(cwdAudio, "*.*")
                        .Where(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    var bg1 = cwdFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals("bg1", StringComparison.OrdinalIgnoreCase));
                    if (bg1 != null)
                    {
                        AmbientMusicPath = bg1;
                    }
                }
            }
        }

        public void PlaySound(string relativeOrFullPath, float volume = 1.0f, float pitch = 1.0f)
        {
            if (!isInitialized) return;

            string fullPath = ResolveAudioPath(relativeOrFullPath);
            if (!File.Exists(fullPath)) return;

            int buffer = GetOrCreateBuffer(fullPath);
            if (buffer == 0) return;

            // Pick next source from pool
            int source = sfxSources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % SOURCE_POOL_SIZE;

            AL.SourceStop(source);
            AL.Sourcei(source, SourcePNameI.Buffer, buffer);
            AL.Sourcef(source, SourcePNameF.Gain, Math.Clamp(volume * SoundVolume, 0f, 1f));
            AL.Sourcef(source, SourcePNameF.Pitch, Math.Clamp(pitch, 0.5f, 2.0f));
            AL.Sourcei(source, SourcePNameI.Looping, 0);
            AL.SourcePlay(source);
        }

        public void PlayCategorySound(string category, float volume = 1.0f, float pitchVariance = 0.08f)
        {
            category = category.ToLowerInvariant();
            if (soundCategoryFiles.TryGetValue(category, out var files) && files.Length > 0)
            {
                string chosen = files[random.Next(files.Length)];
                float pitch = 1.0f + ((float)random.NextDouble() * 2f - 1f) * pitchVariance;
                PlaySound(chosen, volume, pitch);
            }
        }

        // Minecraft Block SFX mapping
        public void PlayBlockStepSound(BlockType blockType)
        {
            string category = GetSoundCategoryForBlock(blockType);
            PlayCategorySound(category, volume: 0.35f, pitchVariance: 0.12f);
        }

        public void PlayBlockBreakSound(BlockType blockType)
        {
            string category = GetSoundCategoryForBlock(blockType);
            PlayCategorySound(category, volume: 0.90f, pitchVariance: 0.10f);
        }

        public void PlayBlockPlaceSound(BlockType blockType)
        {
            string category = GetSoundCategoryForBlock(blockType);
            PlayCategorySound(category, volume: 0.85f, pitchVariance: 0.08f);
        }

        public static string GetSoundCategoryForBlock(BlockType blockType)
        {
            return blockType switch
            {
                BlockType.Grass or BlockType.Leaves or BlockType.SpruceLeaves or BlockType.Rose or
                BlockType.Dandelion or BlockType.OakSapling or BlockType.Wheat or BlockType.SugarCane or
                BlockType.Cactus or BlockType.RedMushroom or BlockType.BrownMushroom or
                BlockType.TallGrass or BlockType.WheatSeeds => "grass",

                BlockType.Dirt or BlockType.Sand or BlockType.Gravel or BlockType.Clay or
                BlockType.SoulSand or BlockType.Farmland or BlockType.SnowBlock or BlockType.SnowyGrass or
                BlockType.WhiteWool or BlockType.OrangeWool or BlockType.MagentaWool or BlockType.LightBlueWool or
                BlockType.YellowWool or BlockType.LimeWool or BlockType.PinkWool or BlockType.GrayWool or
                BlockType.LightGrayWool or BlockType.CyanWool or BlockType.PurpleWool or BlockType.BlueWool or
                BlockType.BrownWool or BlockType.GreenWool or BlockType.RedWool or BlockType.BlackWool => "gravel",

                BlockType.Stone or BlockType.Cobblestone or BlockType.Bedrock or BlockType.CoalOre or
                BlockType.IronOre or BlockType.GoldOre or BlockType.DiamondOre or BlockType.RedstoneOre or
                BlockType.LapisOre or BlockType.MossyCobblestone or BlockType.Bricks or BlockType.SmoothStone or
                BlockType.DoubleStoneSlab or BlockType.Sandstone or BlockType.Netherrack or BlockType.Furnace or
                BlockType.LitFurnace or BlockType.Dispenser or BlockType.MonsterSpawner or BlockType.Obsidian or
                BlockType.StoneButton or BlockType.StoneButtonPressed => "concrete",

                BlockType.Log or BlockType.Planks or BlockType.BirchLog or BlockType.SpruceLog or
                BlockType.DoorLower or BlockType.DoorUpper or BlockType.DoorLowerOpen or BlockType.DoorUpperOpen or
                BlockType.Chest or BlockType.CraftingTable or BlockType.Bookshelf or BlockType.Jukebox or
                BlockType.Pumpkin or BlockType.CarvedPumpkin or BlockType.JackOLantern or BlockType.TNT or
                BlockType.Trapdoor or BlockType.Ladder or BlockType.Cake or BlockType.Sponge or
                BlockType.Glass or BlockType.Ice or BlockType.BedFoot or BlockType.BedHead => "wood",

                BlockType.Water or BlockType.Portal => "water",
                BlockType.Lava => "water",

                BlockType.Rail or BlockType.PoweredRail or BlockType.PoweredRailOn or BlockType.IronIngot or
                BlockType.IronBlock or BlockType.GoldBlock or BlockType.DiamondBlock or BlockType.LapisBlock or
                BlockType.RedstoneBlock or BlockType.CoalBlock or
                BlockType.IronDoorLower or BlockType.IronDoorUpper or BlockType.IronDoorLowerOpen or BlockType.IronDoorUpperOpen => "metal",

                _ => "gravel"
            };
        }

        public void PlayPlayerJump()
        {
            if (soundCategoryFiles.TryGetValue("player", out var files))
            {
                var jumpFile = files.FirstOrDefault(f => f.Contains("Jump", StringComparison.OrdinalIgnoreCase));
                if (jumpFile != null)
                {
                    PlaySound(jumpFile, 0.6f);
                    return;
                }
            }
            PlayCategorySound("concrete", volume: 0.5f, pitchVariance: 0.05f);
        }

        public void PlayPlayerLand()
        {
            if (soundCategoryFiles.TryGetValue("player", out var files))
            {
                var landFile = files.FirstOrDefault(f => f.Contains("Land", StringComparison.OrdinalIgnoreCase));
                if (landFile != null)
                {
                    PlaySound(landFile, 0.7f);
                    return;
                }
            }
            PlayCategorySound("concrete", volume: 0.7f);
        }

        public void PlayPlayerHurt()
        {
            if (soundCategoryFiles.TryGetValue("player", out var files))
            {
                var hurtFile = files.FirstOrDefault(f => f.Contains("Land", StringComparison.OrdinalIgnoreCase));
                if (hurtFile != null)
                {
                    PlaySound(hurtFile, 0.85f, 0.75f);
                    return;
                }
            }
            PlayCategorySound("concrete", volume: 0.8f, pitchVariance: 0.2f);
        }

        public void PlayPortalIgnite()
        {
            PlayCategorySound("metal", volume: 1.0f, pitchVariance: 0.15f);
        }

        public void PlayPortalTravel()
        {
            PlayCategorySound("water", volume: 1.0f, pitchVariance: 0.1f);
        }

        // Music & Ambient Playback
        public void PlayMusic(string relativeOrFullPath, bool loop = true)
        {
            if (!isInitialized) return;

            string fullPath = ResolveAudioPath(relativeOrFullPath);
            if (!File.Exists(fullPath)) return;

            if (string.Equals(currentPlayingMusic, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                // Ensure it is actively playing if it was already selected
                int state = AL.GetSourcei(musicSource, (SourceGetPNameI)0x1010);
                if (state != 0x1012) // AL_PLAYING = 0x1012
                {
                    AL.SourcePlay(musicSource);
                }
                return;
            }

            int buffer = GetOrCreateBuffer(fullPath);
            if (buffer == 0) return;

            AL.SourceStop(musicSource);
            AL.Sourcei(musicSource, SourcePNameI.Buffer, buffer);
            AL.Sourcef(musicSource, SourcePNameF.Gain, Math.Clamp(MusicVolume, 0f, 1f));
            AL.Sourcei(musicSource, SourcePNameI.Looping, loop ? 1 : 0);
            AL.SourcePlay(musicSource);

            currentPlayingMusic = fullPath;
        }

        public void StopMusic()
        {
            if (!isInitialized) return;
            AL.SourceStop(musicSource);
            currentPlayingMusic = null;
        }

        public void Update(DayNightPhase phase, float deltaTime)
        {
            if (!isInitialized) return;

            // Ensure background music (bg1) is playing continuously all the time
            if (!string.IsNullOrEmpty(AmbientMusicPath))
            {
                int state = AL.GetSourcei(musicSource, (SourceGetPNameI)0x1010);
                if (state != 0x1012) // AL_PLAYING = 0x1012
                {
                    PlayMusic(AmbientMusicPath, loop: true);
                }
            }
            else if (phase != lastObservedPhase)
            {
                lastObservedPhase = phase;

                if (phase == DayNightPhase.Day || phase == DayNightPhase.Dawn)
                {
                    if (!string.IsNullOrEmpty(DayMusicPath) && File.Exists(ResolveAudioPath(DayMusicPath)))
                    {
                        PlayMusic(DayMusicPath, loop: true);
                    }
                }
                else if (phase == DayNightPhase.Night || phase == DayNightPhase.Dusk)
                {
                    if (!string.IsNullOrEmpty(NightMusicPath) && File.Exists(ResolveAudioPath(NightMusicPath)))
                    {
                        PlayMusic(NightMusicPath, loop: true);
                    }
                }
            }

            // Sync volume updates
            if (musicSource != 0)
            {
                AL.Sourcef(musicSource, SourcePNameF.Gain, Math.Clamp(MusicVolume, 0f, 1f));
            }
        }

        private int GetOrCreateBuffer(string filePath)
        {
            if (bufferCache.TryGetValue(filePath, out int cached))
            {
                return cached;
            }

            byte[] audioData;
            Format format;
            int sampleRate;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".mp3")
            {
                if (!LoadMp3File(filePath, out audioData, out format, out sampleRate))
                {
                    Console.WriteLine($"[Audio] Warning: Failed to decode MP3 file '{filePath}'");
                    return 0;
                }
            }
            else
            {
                if (!LoadWavFile(filePath, out audioData, out format, out sampleRate))
                {
                    Console.WriteLine($"[Audio] Warning: Failed to decode WAV file '{filePath}'");
                    return 0;
                }
            }

            int buffer = AL.GenBuffer();
            unsafe
            {
                fixed (byte* ptr = audioData)
                {
                    AL.BufferData(buffer, format, (IntPtr)ptr, audioData.Length, sampleRate);
                }
            }

            bufferCache[filePath] = buffer;
            return buffer;
        }

        private static bool LoadMp3File(string filePath, out byte[] audioData, out Format format, out int sampleRate)
        {
            audioData = Array.Empty<byte>();
            format = Format.Stereo16;
            sampleRate = 44100;

            try
            {
                using var mpeg = new NLayer.MpegFile(filePath);
                sampleRate = mpeg.SampleRate;
                format = mpeg.Channels == 1 ? Format.Mono16 : Format.Stereo16;

                using var ms = new MemoryStream();
                byte[] buffer = new byte[16384];
                int read;
                while ((read = mpeg.ReadSamplesInt16(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                audioData = ms.ToArray();
                return audioData.Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Warning: Failed to decode MP3 file '{filePath}': {ex.Message}");
                return false;
            }
        }

        private string ResolveAudioPath(string path)
        {
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string direct = Path.Combine(baseDir, path);
            if (File.Exists(direct)) return direct;

            string inAudio = Path.Combine(baseDir, "Audio", path);
            if (File.Exists(inAudio)) return inAudio;

            string inCwd = Path.Combine(Directory.GetCurrentDirectory(), path);
            if (File.Exists(inCwd)) return inCwd;

            string inCwdAudio = Path.Combine(Directory.GetCurrentDirectory(), "Audio", path);
            if (File.Exists(inCwdAudio)) return inCwdAudio;

            return path;
        }

        // Lightweight pure C# RIFF WAV parser (supports 8-bit & 16-bit PCM, mono & stereo)
        private static bool LoadWavFile(string filePath, out byte[] audioData, out Format format, out int sampleRate)
        {
            audioData = Array.Empty<byte>();
            format = Format.Mono16;
            sampleRate = 44100;

            try
            {
                using var fs = File.OpenRead(filePath);
                using var reader = new BinaryReader(fs);

                // Read RIFF header
                string riff = new string(reader.ReadChars(4));
                if (riff != "RIFF") return false;

                reader.ReadInt32(); // fileSize - 8

                string wave = new string(reader.ReadChars(4));
                if (wave != "WAVE") return false;

                short channels = 1;
                short bitsPerSample = 16;
                short audioFormat = 1;
                bool fmtFound = false;
                bool dataFound = false;

                while (fs.Position < fs.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        audioFormat = reader.ReadInt16(); // 1 = PCM, 3 = IEEE float
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32(); // byteRate
                        reader.ReadInt16(); // blockAlign
                        bitsPerSample = reader.ReadInt16();

                        // Skip any extra format bytes
                        int extra = chunkSize - 16;
                        if (extra > 0) fs.Seek(extra, SeekOrigin.Current);
                        fmtFound = true;
                    }
                    else if (chunkId == "data")
                    {
                        audioData = reader.ReadBytes(chunkSize);
                        dataFound = true;
                        break;
                    }
                    else
                    {
                        // Skip unrecognized chunks (e.g. LIST, JUNK)
                        fs.Seek(chunkSize, SeekOrigin.Current);
                    }
                }

                if (!fmtFound || !dataFound) return false;

                // Normalize all audio bit-depths cleanly to 16-bit signed PCM
                if (bitsPerSample == 24)
                {
                    // 24-bit PCM (3 bytes per sample) -> convert to standard 16-bit signed PCM (2 bytes)
                    int totalSamples = audioData.Length / 3;
                    byte[] pcm16 = new byte[totalSamples * 2];
                    for (int i = 0; i < totalSamples; i++)
                    {
                        // In 24-bit little endian: byte 0 = lowest 8 bits, byte 1 = mid, byte 2 = high (sign)
                        // Discard the lowest 8 bits of noise floor, take the top 16 bits:
                        pcm16[i * 2] = audioData[i * 3 + 1];
                        pcm16[i * 2 + 1] = audioData[i * 3 + 2];
                    }
                    audioData = pcm16;
                }
                else if (bitsPerSample == 32)
                {
                    int totalSamples = audioData.Length / 4;
                    byte[] pcm16 = new byte[totalSamples * 2];
                    if (audioFormat == 3) // IEEE 32-bit float
                    {
                        for (int i = 0; i < totalSamples; i++)
                        {
                            float f = BitConverter.ToSingle(audioData, i * 4);
                            short s = (short)(Math.Clamp(f, -1.0f, 1.0f) * 32767f);
                            pcm16[i * 2] = (byte)(s & 0xFF);
                            pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                        }
                    }
                    else // 32-bit integer PCM
                    {
                        for (int i = 0; i < totalSamples; i++)
                        {
                            pcm16[i * 2] = audioData[i * 4 + 2];
                            pcm16[i * 2 + 1] = audioData[i * 4 + 3];
                        }
                    }
                    audioData = pcm16;
                }
                else if (bitsPerSample == 8)
                {
                    // 8-bit unsigned PCM: convert to 16-bit signed PCM
                    int totalSamples = audioData.Length;
                    byte[] pcm16 = new byte[totalSamples * 2];
                    for (int i = 0; i < totalSamples; i++)
                    {
                        short s = (short)((audioData[i] - 128) * 256);
                        pcm16[i * 2] = (byte)(s & 0xFF);
                        pcm16[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
                    }
                    audioData = pcm16;
                }

                format = (channels == 1) ? Format.Mono16 : Format.Stereo16;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Error reading WAV '{filePath}': {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            if (!isInitialized) return;

            try
            {
                if (musicSource != 0)
                {
                    AL.SourceStop(musicSource);
                    AL.DeleteSource(musicSource);
                }

                for (int i = 0; i < SOURCE_POOL_SIZE; i++)
                {
                    if (sfxSources[i] != 0)
                    {
                        AL.SourceStop(sfxSources[i]);
                        AL.DeleteSource(sfxSources[i]);
                    }
                }

                foreach (var buf in bufferCache.Values)
                {
                    AL.DeleteBuffer(buf);
                }
                bufferCache.Clear();

                ALC.MakeContextCurrent(default);
                ALC.DestroyContext(context);
                ALC.CloseDevice(device);
            }
            catch { }

            isInitialized = false;
        }
    }
}
