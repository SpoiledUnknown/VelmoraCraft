using OpenTK.Mathematics;

namespace VelmoraCraft.src.World;

public enum DayNightPhase
{
    Dawn, // 1 min 30 sec (90s)
    Day, // 10 mins (600s)
    Dusk, // 1 min 30 sec (90s)
    Night // 7 mins (420s)
}

public class DayNightCycle
{
    // Minecraft 20-minute cycle timings
    public const float DAWN_DURATION = 90.0f; // 1m 30s
    public const float DAY_DURATION = 600.0f; // 10m
    public const float DUSK_DURATION = 90.0f; // 1m 30s
    public const float NIGHT_DURATION = 420.0f; // 7m

    public const float
        TOTAL_CYCLE_DURATION = DAWN_DURATION + DAY_DURATION + DUSK_DURATION + NIGHT_DURATION; // 1200.0f (20 minutes)

    public DayNightCycle(float startTimeSeconds = 150.0f, float dayDuration = TOTAL_CYCLE_DURATION)
    {
        DayDuration = dayDuration;
        TimeOfDay = startTimeSeconds % dayDuration;
        Update(0.0f);
    }

    // Cycle settings
    public float TimeOfDay { get; set; } = DAWN_DURATION + 60.0f; // Start 1 minute into bright daylight
    public float DayDuration { get; set; } = TOTAL_CYCLE_DURATION;
    public float TimeScale { get; set; } = 1.0f;
    public bool IsPaused { get; set; } = false;
    public int DaysSurvived { get; set; }

    // Current properties computed each frame
    public float NormalizedTime => TimeOfDay % DayDuration / DayDuration; // 0.0 to 1.0
    public DayNightPhase CurrentPhase { get; private set; } = DayNightPhase.Day;

    public Vector3 SunDirection { get; private set; }
    public Vector3 MoonDirection { get; private set; }
    public Vector3 ActiveLightDirection { get; private set; }
    public Vector3 ActiveLightColor { get; private set; }
    public float ActiveLightIntensity { get; private set; }
    public Vector3 AmbientColor { get; private set; }
    public Vector3 SkyColor { get; private set; }

    public void Update(float deltaTime)
    {
        if (!IsPaused && deltaTime > 0.0f)
        {
            var newTime = TimeOfDay + deltaTime * TimeScale;
            if (newTime >= DayDuration) DaysSurvived += (int)(newTime / DayDuration);
            TimeOfDay = newTime % DayDuration;
        }

        var currentSec = TimeOfDay % DayDuration;

        // Determine exact Minecraft phase
        if (currentSec < DAWN_DURATION)
            CurrentPhase = DayNightPhase.Dawn;
        else if (currentSec < DAWN_DURATION + DAY_DURATION)
            CurrentPhase = DayNightPhase.Day;
        else if (currentSec < DAWN_DURATION + DAY_DURATION + DUSK_DURATION)
            CurrentPhase = DayNightPhase.Dusk;
        else
            CurrentPhase = DayNightPhase.Night;

        var t = NormalizedTime;

        // Celestial rotation across 24h cycle
        var sunAngle = (t - 0.25f) * MathF.Tau;

        var sunY = MathF.Cos(sunAngle);
        var sunX = -MathF.Sin(sunAngle);
        var sunZ = 0.25f;

        SunDirection = Vector3.Normalize(new Vector3(sunX, sunY, sunZ));
        MoonDirection = -SunDirection;

        // Sky & ambient colors based on phase
        SkyColor = ComputeSkyColor(CurrentPhase, currentSec);
        AmbientColor = ComputeAmbientColor(CurrentPhase, currentSec);

        // Sun vs Moon directional lighting
        if (SunDirection.Y > -0.05f)
        {
            // Day / Sunset / Dawn -> Sun is active light
            ActiveLightDirection = SunDirection;
            var sunHeight = Math.Clamp(SunDirection.Y, 0.0f, 1.0f);

            // Transition light color from golden/reddish at horizon to warm daylight overhead
            var dawnLight = new Vector3(1.0f, 0.55f, 0.30f);
            var noonLight = new Vector3(1.0f, 0.98f, 0.90f);
            ActiveLightColor = Vector3.Lerp(dawnLight, noonLight, MathF.Pow(sunHeight, 0.6f));
            ActiveLightIntensity = MathF.Max(0.12f, sunHeight * 0.95f);
        }
        else
        {
            // Night -> Moon is active light
            ActiveLightDirection = MoonDirection;
            var moonHeight = Math.Clamp(MoonDirection.Y, 0.0f, 1.0f);
            ActiveLightColor = new Vector3(0.25f, 0.35f, 0.55f); // Soft cool blue moonlight
            ActiveLightIntensity = MathF.Max(0.06f, moonHeight * 0.30f);
        }
    }

    private static Vector3 ComputeSkyColor(DayNightPhase phase, float sec)
    {
        switch (phase)
        {
            case DayNightPhase.Dawn:
                // 90s sunrise transition: Deep night -> rosy morning gold -> azure day
                var fDawn = sec / DAWN_DURATION;
                return Vector3.Lerp(new Vector3(0.06f, 0.08f, 0.18f), new Vector3(0.85f, 0.55f, 0.38f),
                    MathF.Sin(fDawn * MathF.PI * 0.5f));

            case DayNightPhase.Day:
                // 10m clear bright blue sky with midday azure
                var fDay = (sec - DAWN_DURATION) / DAY_DURATION;
                return Vector3.Lerp(new Vector3(0.60f, 0.76f, 0.95f), new Vector3(0.50f, 0.72f, 0.96f),
                    MathF.Sin(fDay * MathF.PI));

            case DayNightPhase.Dusk:
                // 1m 30s sunset: Day blue -> radiant orange/crimson -> twilight violet
                var fDusk = (sec - (DAWN_DURATION + DAY_DURATION)) / DUSK_DURATION;
                return Vector3.Lerp(new Vector3(0.88f, 0.42f, 0.22f), new Vector3(0.16f, 0.12f, 0.26f), fDusk);

            case DayNightPhase.Night:
            default:
                // 7m deep starry indigo / midnight blue
                var fNight = (sec - (DAWN_DURATION + DAY_DURATION + DUSK_DURATION)) / NIGHT_DURATION;
                return Vector3.Lerp(new Vector3(0.14f, 0.10f, 0.22f), new Vector3(0.03f, 0.04f, 0.09f),
                    MathF.Sin(fNight * MathF.PI));
        }
    }

    private static Vector3 ComputeAmbientColor(DayNightPhase phase, float sec)
    {
        switch (phase)
        {
            case DayNightPhase.Dawn:
                var fDawn = sec / DAWN_DURATION;
                return Vector3.Lerp(new Vector3(0.12f, 0.13f, 0.18f), new Vector3(0.38f, 0.40f, 0.44f), fDawn);

            case DayNightPhase.Day:
                return new Vector3(0.42f, 0.44f, 0.48f);

            case DayNightPhase.Dusk:
                var fDusk = (sec - (DAWN_DURATION + DAY_DURATION)) / DUSK_DURATION;
                return Vector3.Lerp(new Vector3(0.40f, 0.38f, 0.35f), new Vector3(0.12f, 0.13f, 0.18f), fDusk);

            case DayNightPhase.Night:
            default:
                return new Vector3(0.10f, 0.12f, 0.18f);
        }
    }

    public string GetTimeString()
    {
        var hour24 = NormalizedTime * 24.0f;
        var hour = (int)hour24;
        var minute = (int)((hour24 - hour) * 60.0f);
        var period = hour >= 12 ? "PM" : "AM";
        var displayHour = hour % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour:D2}:{minute:D2} {period} ({CurrentPhase})";
    }
}