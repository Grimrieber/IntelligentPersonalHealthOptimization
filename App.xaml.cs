namespace IntelligentPersonalHealthOptimization;

public partial class App : Application
{
    private const string SessionKey = "session_active";
    private const string SessionTimeKey = "session_last_active";

    // Auto-lock after 5 minutes of inactivity
    public static readonly TimeSpan AutoLockTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Check if session is still valid (authenticated and within timeout).
    /// Uses Preferences so it survives Android process death.
    /// </summary>
    public static bool HasValidSession
    {
        get
        {
            try
            {
                var isAuth = Preferences.Get(SessionKey, false);
                if (!isAuth) return false;

                var lastActiveTicks = Preferences.Get(SessionTimeKey, 0L);
                if (lastActiveTicks == 0) return false;

                var lastActive = new DateTime(lastActiveTicks, DateTimeKind.Utc);
                var elapsed = DateTime.UtcNow - lastActive;

                if (elapsed > AutoLockTimeout)
                {
                    // Session expired
                    Preferences.Set(SessionKey, false);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Mark the session as authenticated and refresh the timestamp.
    /// </summary>
    public static void TouchSession()
    {
        try
        {
            Preferences.Set(SessionKey, true);
            Preferences.Set(SessionTimeKey, DateTime.UtcNow.Ticks);
        }
        catch { }
    }

    /// <summary>
    /// End the session (logout or timeout).
    /// </summary>
    public static void EndSession()
    {
        try
        {
            Preferences.Set(SessionKey, false);
            Preferences.Remove(SessionTimeKey);
        }
        catch { }
    }

    public App()
    {
        InitializeComponent();

        // Global exception handlers — log to persistent file so we can review after crashes
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Services.CrashLogger.LogFatal("AppDomain.UnhandledException", e.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Services.CrashLogger.Log("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved(); // Prevent crash from unobserved task exceptions
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
