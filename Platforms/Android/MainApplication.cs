using Android.App;
using Android.Runtime;

namespace IntelligentPersonalHealthOptimization
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            // Catch Java/Android-level exceptions that .NET handlers miss
            Java.Lang.Thread.DefaultUncaughtExceptionHandler = new CrashHandler(
                Java.Lang.Thread.DefaultUncaughtExceptionHandler);

            // Catch .NET exceptions on Android
            AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
            {
                IntelligentPersonalHealthOptimization.Services.CrashLogger.LogFatal("AndroidEnvironment.UnhandledExceptionRaiser", e.Exception);
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

    /// <summary>
    /// Catches Java-level unhandled exceptions and logs them before the app dies.
    /// </summary>
    public class CrashHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
    {
        private readonly Java.Lang.Thread.IUncaughtExceptionHandler? _defaultHandler;

        public CrashHandler(Java.Lang.Thread.IUncaughtExceptionHandler? defaultHandler)
        {
            _defaultHandler = defaultHandler;
        }

        public void UncaughtException(Java.Lang.Thread t, Java.Lang.Throwable e)
        {
            try
            {
                IntelligentPersonalHealthOptimization.Services.CrashLogger.LogFatal($"Java.UncaughtException (thread: {t.Name})", e.ToString());
            }
            catch
            {
                // Last resort — can't even log
            }

            // Pass to the default handler so Android can show the crash dialog
            _defaultHandler?.UncaughtException(t, e);
        }
    }
}
