using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Data;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class DatabaseService : IDatabaseService
{
    private SQLiteAsyncConnection? _connection;
    // True only AFTER tables + seed + all one-shot migrations finish. Callers must gate on
    // this, NOT on `_connection != null` — the connection is assigned before migrations run,
    // so keying readiness off the field lets a concurrent caller query half-migrated data
    // (e.g. the Recipes page reading a recipe before ApplyRecipeTimeCleanupAsync committed).
    private volatile bool _initialized;
    private readonly ISecurityService _securityService;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public DatabaseService(ISecurityService securityService)
    {
        _securityService = securityService;
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, AppConstants.DatabaseName);
            var key = await _securityService.GetOrCreateDatabaseKeyAsync();
            var isNewDb = !File.Exists(dbPath);

            var options = new SQLiteConnectionString(dbPath, true, key: key);
            _connection = new SQLiteAsyncConnection(options);

            try
            {
                await CreateTablesAndSeedAsync();
            }
            catch (Exception) when (isNewDb)
            {
                // Only delete and recreate if this was a brand new database (no user data to lose).
                // If the DB already existed, the tables/seed likely partially succeeded — don't nuke user data.
                try { await _connection.CloseAsync(); } catch { /* ignore close errors */ }
                _connection = null;

                if (File.Exists(dbPath))
                    File.Delete(dbPath);

                options = new SQLiteConnectionString(dbPath, true, key: key);
                _connection = new SQLiteAsyncConnection(options);
                await CreateTablesAndSeedAsync();
            }

            // Only now is the DB fully migrated — expose it to callers. If seeding threw
            // (and wasn't recoverable above), this is skipped so the next call retries.
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task CreateTablesAndSeedAsync()
    {
        // Core tables
        await _connection!.CreateTableAsync<User>();
        await _connection.CreateTableAsync<AssessmentSession>();
        await _connection.CreateTableAsync<AssessmentResult>();
        await _connection.CreateTableAsync<Exercise>();
        await _connection.CreateTableAsync<WorkoutProgram>();
        await _connection.CreateTableAsync<WorkoutDay>();
        await _connection.CreateTableAsync<WorkoutExercise>();
        await _connection.CreateTableAsync<ProgressEntry>();
        await _connection.CreateTableAsync<BodyMeasurement>();

        // Training profile
        await _connection.CreateTableAsync<TrainingProfile>();

        // Expanded assessments
        await _connection.CreateTableAsync<PostureAssessment>();
        await _connection.CreateTableAsync<FlexibilityAssessment>();
        await _connection.CreateTableAsync<FitnessBenchmark>();

        // Nutrition
        await _connection.CreateTableAsync<NutritionProfile>();
        await _connection.CreateTableAsync<Food>();
        await _connection.CreateTableAsync<MealPlan>();
        await _connection.CreateTableAsync<MealPlanDay>();
        await _connection.CreateTableAsync<MealPlanItem>();
        await _connection.CreateTableAsync<FoodLogEntry>();
        await _connection.CreateTableAsync<WaterLog>();

        // Nutrition Assessment
        await _connection.CreateTableAsync<NutritionAssessment>();

        // Wellness Check-In (Eating Disorder Screening)
        await _connection.CreateTableAsync<WellnessCheckIn>();

        // CES Assessment
        await _connection.CreateTableAsync<CesAssessment>();

        // Goals
        await _connection.CreateTableAsync<UserGoal>();
        await _connection.CreateTableAsync<GoalMilestone>();

        // Schedule
        await _connection.CreateTableAsync<ScheduledWorkout>();
        await _connection.CreateTableAsync<WorkoutCompletion>();

        // Saved Recipes (local copies from MSSQL cookbook)
        await _connection.CreateTableAsync<SavedRecipe>();
        await _connection.CreateTableAsync<SavedRecipeIngredient>();
        await _connection.CreateTableAsync<SavedRecipeDirection>();

        // Equipment intel (post-CES inventory + environment + gym-chain presets)
        await _connection.CreateTableAsync<EquipmentInventoryItem>();
        await _connection.CreateTableAsync<TrainingEnvironment>();
        await _connection.CreateTableAsync<GymChainEquipmentTemplate>();

        // Strength baselines + per-set logging
        await _connection.CreateTableAsync<WorkingWeight>();
        await _connection.CreateTableAsync<ExercisePerformanceEntry>();

        // Local notification preferences
        await _connection.CreateTableAsync<NotificationSettings>();

        // Seed data
        await SeedData.SeedExercisesAsync(_connection);
        await SeedData.SeedCesExercisesAsync(_connection);
        await SeedData.SeedFoodsAsync(_connection);
        await SeedData.SeedGymChainTemplatesAsync(_connection);
        await SeedData.SeedWikibooksRecipesAsync(_connection);

        // One-shot DB fix for incorrect onboarding values. Self-gated; runs once.
        await SeedData.ApplyOneShotProfileFixAsync(_connection);

        // One-shot removal of unusable cookbook recipes. Self-gated; runs once.
        await SeedData.ApplyRecipeCleanupAsync(_connection);

        // One-shot: give "Uncategorized" recipes a real category from their name.
        await SeedData.ApplyRecipeRecategorizeAsync(_connection);

        // One-shot: remove inappropriate / non-food recipes flagged by audit.
        await SeedData.ApplyQuestionableRecipeRemovalAsync(_connection);

        // One-shot: purge duplicate "User" recipe rows left by the old meal-plan
        // pool builder (now fixed to read the bundled catalog directly).
        await SeedData.ApplyDuplicateRecipeCleanupAsync(_connection);

        // One-shot: backfill servings + per-serving nutrition for catalog recipes
        // that shipped without them (refreshes existing rows from the new bundle,
        // preserving favorites). Self-gated; runs once.
        await SeedData.ApplyServingsBackfillAsync(_connection);

        // One-shot: drop catalog recipes the nutrition audit excluded (no usable
        // calories) and refresh servings/nutrition for the rest from the cleaned
        // bundle. Self-gated; runs once.
        await SeedData.ApplyRecipeAuditFixAsync(_connection);

        // One-shot: delete non-meal catalog recipes (drinks, sauces, spice mixes…) to
        // save space. Deterministic — purges by category, independent of the bundle.
        await SeedData.ApplyNonMealPurgeAsync(_connection);

        // One-shot: collapse duplicate TrainingProfile rows so the training level is consistent.
        await SeedData.ApplyTrainingProfileDedupAsync(_connection);

        // One-shot: re-sync catalog to the pruned bundle — deletes the 107 same-dish
        // duplicate recipes (Meatloaf II–V, etc.) removed from the bundle. Self-gated.
        await SeedData.ApplyDuplicateDishPurgeAsync(_connection);

        // One-shot: backfill recipe image URLs from the (re-exported) bundle for
        // existing installs. New installs get them straight from the seeder.
        await SeedData.ApplyRecipeImagesAsync(_connection);

        // One-shot: re-sync prep/cook/rest times from the cleaned bundle — the scrape had
        // jammed "Prep: X Cooking: Y Total: Z" into one cookTime string on ~151 recipes.
        await SeedData.ApplyRecipeTimeCleanupAsync(_connection);

        // One-shot: classify catalog recipes on a fitness health lens (HealthScore/
        // HealthTier) and purge the clearest junk (cake/candy/sugar bombs). Computed
        // on-device from the seeded nutrition — no bundle/server. Self-gated.
        await SeedData.ApplyHealthClassificationAsync(_connection);
    }

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        // Fast path: skip semaphore only once init (tables + seed + migrations) is fully done.
        // Gating on _initialized (not _connection) prevents handing out a connection whose
        // one-shot migrations haven't committed yet — that caused a first-launch race where
        // the Recipes list showed stale data until re-navigation.
        if (_initialized)
            return _connection!;

        await InitializeAsync();
        return _connection!;
    }

    public async Task<List<T>> GetAllAsync<T>() where T : new()
    {
        var db = await GetConnectionAsync();
        return await db.Table<T>().ToListAsync();
    }

    public async Task<T?> GetByIdAsync<T>(int id) where T : class, new()
    {
        var db = await GetConnectionAsync();
        return await db.FindAsync<T>(id);
    }

    public async Task<int> InsertAsync<T>(T entity) where T : new()
    {
        var db = await GetConnectionAsync();
        return await db.InsertAsync(entity);
    }

    public async Task<int> UpdateAsync<T>(T entity) where T : new()
    {
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(entity);
    }

    public async Task<int> DeleteAsync<T>(T entity) where T : new()
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync(entity);
    }

    public async Task<List<T>> QueryAsync<T>(string query, params object[] args) where T : new()
    {
        var db = await GetConnectionAsync();
        return await db.QueryAsync<T>(query, args);
    }

    public async Task RunInTransactionAsync(Action<SQLiteConnection> action)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(action);
    }
}
