namespace IntelligentPersonalHealthOptimization.Constants;

public static class AppConstants
{
    public const string DatabaseName = "health_app.db3";
    public const string DatabaseKeyStorageKey = "db_encryption_key";
    public const string PinHashStorageKey = "user_pin_hash";
    public const string PinSaltStorageKey = "user_pin_salt";
    public const string SecurityQuestionStorageKey = "security_question";
    public const string SecurityAnswerHashStorageKey = "security_answer_hash";
    public const string SecurityAnswerSaltStorageKey = "security_answer_salt";
    public const string BiometricEnabledKey = "biometric_enabled";
    public const string UnitPreferenceKey = "unit_preference";

    public const int MinPinLength = 4;
    public const int MaxPinLength = 6;
    public const int EncryptionKeyLengthBytes = 32;

    public const int MaxCorrectiveExercisesPerWorkout = 4;
    public const int DefaultProgramDurationWeeks = 4;
    public const int DefaultDaysPerWeek = 3;

    // Recipe database connection (MSSQL - direct connection for dev).
    // Set the RECIPEDB_CONNECTION_STRING environment variable on the dev machine; never hardcode.
    public static string RecipeDbConnectionString =>
        Environment.GetEnvironmentVariable("RECIPEDB_CONNECTION_STRING") ?? string.Empty;

    // Recipe API base URL (Android emulator uses 10.0.2.2 to reach host machine)
    public const string RecipeApiBaseUrl =
#if ANDROID
        "http://10.0.2.2:5199";
#elif IOS
        "http://localhost:5199";
#else
        "http://localhost:5199";
#endif
}
