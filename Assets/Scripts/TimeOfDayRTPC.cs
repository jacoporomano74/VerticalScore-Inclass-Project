using UnityEngine;

/// <summary>
/// Runs a simple 24-hour clock and pushes the current hour into a Wwise Game
/// Parameter (RTPC) so audio can react to the time of day.
///
/// Setup:
///   1. In the Wwise project, make a Game Parameter with range 0 - 24
///      (0 = midnight, 12 = noon). Generate SoundBanks.
///   2. Attach this script to the WwiseGlobal GameObject. Wwise adds that
///      object to the open scene automatically; if it is not in the Hierarchy
///      yet, any object that lives for the whole scene works just as well,
///      because Global scope does not care which object hosts the script.
///   3. Pick the Game Parameter in the "Time Of Day Rtpc" field.
///
/// The raw hour (0 - 24) is what gets sent. Shaping it into volumes, filters
/// or blends belongs on the RTPC curves in Wwise, not here.
/// </summary>
public class TimeOfDayRTPC : MonoBehaviour
{
    // Pick this before entering Play mode; it is read during Start.
    public enum RtpcScope
    {
        // Applies everywhere. Correct for time of day, and still reaches
        // voices played on this GameObject.
        Global,

        // Applies only to voices played on this GameObject. Use when you want
        // separate clocks on separate objects.
        ThisGameObject
    }

    [Header("Wwise")]
    [Tooltip("The Wwise Game Parameter to drive. Its range should be 0 - 24.")]
    public AK.Wwise.RTPC timeOfDayRtpc = new AK.Wwise.RTPC();

    public RtpcScope scope = RtpcScope.Global;

    [Header("Clock")]
    [Tooltip("Current time in hours. 0 = midnight, 6.5 = 06:30, 18 = 6pm.\n" +
             "Safe to drag while the game is playing to audition the RTPC.")]
    [Range(0f, 24f)]
    public float currentHour = 6f;

    [Tooltip("Real-world minutes for one full in-game day. 0 freezes the clock.")]
    public float dayLengthMinutes = 10f;

    [Tooltip("Stops the clock without stopping the RTPC updates.")]
    public bool paused;

    [Header("Sending")]
    [Tooltip("Seconds between sends. The sound engine does not need a value every frame.")]
    public float sendInterval = 0.1f;

    [Tooltip("Skip the send if the hour moved less than this. Avoids redundant calls.")]
    public float minHourDelta = 0.01f;

    public bool logSends;

    // Time until the next send is allowed.
    float sendTimer;

    // The last value we actually pushed. NaN so the first send always happens.
    float lastSentHour = float.NaN;

    // True only if we registered this GameObject ourselves, so we know whether
    // it is ours to unregister.
    bool weRegisteredGameObj;

    bool warnedAboutMissingRtpc;

    // Start has run, so AkInitializer has brought the sound engine up.
    bool ready;

    /// <summary>Current time of day in hours, 0 - 24.</summary>
    public float CurrentHour
    {
        get { return currentHour; }
        set { SetHour(value); }
    }

    /// <summary>Current time of day as 0 - 1, for driving non-audio systems.</summary>
    public float NormalizedTime
    {
        get { return currentHour / 24f; }
    }

    void Start()
    {
        // Start, not Awake or OnEnable: those can run before AkInitializer has
        // brought the sound engine up on another GameObject. Start runs after
        // every Awake in the scene, so the engine is ready.
        if (scope == RtpcScope.ThisGameObject)
        {
            RegisterWithSoundEngine();
        }

        // Push an initial value so ambience starts at the right time of day
        // instead of at the Game Parameter's default.
        ready = true;
        Send(force: true);
    }

    void Update()
    {
        AdvanceClock();

        sendTimer -= Time.deltaTime;
        if (sendTimer <= 0f)
        {
            sendTimer = Mathf.Max(sendInterval, 0f);
            Send();
        }
    }

    void OnDestroy()
    {
        if (weRegisteredGameObj)
        {
            AkSoundEngine.UnregisterGameObj(gameObject);
            weRegisteredGameObj = false;
        }
    }

    void OnValidate()
    {
        dayLengthMinutes = Mathf.Max(dayLengthMinutes, 0f);
        sendInterval = Mathf.Max(sendInterval, 0f);
        minHourDelta = Mathf.Max(minHourDelta, 0f);
    }

    /// <summary>Jumps the clock to a specific hour and sends it immediately.</summary>
    public void SetHour(float hour)
    {
        currentHour = Mathf.Repeat(hour, 24f);
        Send(force: true);
    }

    // Moves the clock forward, wrapping past midnight back to 0.
    void AdvanceClock()
    {
        if (paused || dayLengthMinutes <= 0f)
        {
            return;
        }

        float hoursPerSecond = 24f / (dayLengthMinutes * 60f);
        currentHour = Mathf.Repeat(currentHour + hoursPerSecond * Time.deltaTime, 24f);
    }

    // Pushes currentHour to Wwise. Skipped when the value has barely moved,
    // unless forced.
    void Send(bool force = false)
    {
        // SetHour can be called from another script's Awake, before the sound
        // engine exists. Start sends the pending value once it is safe.
        if (!ready)
        {
            return;
        }

        if (!timeOfDayRtpc.IsValid())
        {
            if (!warnedAboutMissingRtpc)
            {
                warnedAboutMissingRtpc = true;
                Debug.LogWarning(
                    "TimeOfDayRTPC on '" + name + "' has no Game Parameter assigned.",
                    this);
            }
            return;
        }

        // float.NaN fails every comparison, so the first call always passes.
        if (!force && Mathf.Abs(currentHour - lastSentHour) < minHourDelta)
        {
            return;
        }

        if (scope == RtpcScope.Global)
        {
            timeOfDayRtpc.SetGlobalValue(currentHour);
        }
        else
        {
            timeOfDayRtpc.SetValue(gameObject, currentHour);
        }

        lastSentHour = currentHour;

        if (logSends)
        {
            Debug.Log("Time of day -> " + FormatClock(currentHour) +
                      " (RTPC " + currentHour.ToString("F2") + ")", this);
        }
    }

    // Per-GameObject RTPCs only apply to objects the sound engine knows about.
    // AkGameObj registers its own object, so only step in when there isn't one.
    void RegisterWithSoundEngine()
    {
        if (GetComponent<AkGameObj>() != null)
        {
            return;
        }

        AkSoundEngine.RegisterGameObj(gameObject, name);
        weRegisteredGameObj = true;
    }

    // 6.5 -> "06:30"
    static string FormatClock(float hour)
    {
        int hours = Mathf.FloorToInt(hour) % 24;
        int minutes = Mathf.FloorToInt(hour * 60f) % 60;
        return hours.ToString("00") + ":" + minutes.ToString("00");
    }
}
