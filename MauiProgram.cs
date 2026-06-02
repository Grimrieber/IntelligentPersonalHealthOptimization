using CommunityToolkit.Maui;
using IntelligentPersonalHealthOptimization.Services.Implementation;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using IntelligentPersonalHealthOptimization.ViewModels;
using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;
using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;
using IntelligentPersonalHealthOptimization.Views;
using IntelligentPersonalHealthOptimization.Views.Assessment;
using IntelligentPersonalHealthOptimization.Views.Goals;
using IntelligentPersonalHealthOptimization.Views.Nutrition;
using IntelligentPersonalHealthOptimization.Views.NutritionAssessment;
using IntelligentPersonalHealthOptimization.Views.Onboarding;
using IntelligentPersonalHealthOptimization.Views.Progress;
using IntelligentPersonalHealthOptimization.Views.Recipes;
using IntelligentPersonalHealthOptimization.Views.Schedule;
using IntelligentPersonalHealthOptimization.Views.Settings;
using IntelligentPersonalHealthOptimization.Views.CesAssessment;
using IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;
using IntelligentPersonalHealthOptimization.Views.Workout;
using CesVm = IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;
using IntelligentPersonalHealthOptimization.ViewModels.WellnessCheckIn;
using IntelligentPersonalHealthOptimization.ViewModels.Workout;
using Microcharts.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;

namespace IntelligentPersonalHealthOptimization;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement()
            .UseMicrocharts()
            .UseLocalNotification()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ===== Services (Singletons) =====
        builder.Services.AddSingleton<ISecurityService, SecurityService>();
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton<IAssessmentService, AssessmentService>();
        builder.Services.AddSingleton<IPrescriptionEngine, PrescriptionEngine>();
        builder.Services.AddSingleton<IProgressService, ProgressService>();
        builder.Services.AddSingleton<IMealPlanRecipeService, MealPlanRecipeService>();
        builder.Services.AddSingleton<INutritionService, NutritionService>();
        builder.Services.AddSingleton<IFoodService, FoodService>();
        builder.Services.AddSingleton<IGoalService, GoalService>();
        builder.Services.AddSingleton<IScheduleService, ScheduleService>();
        builder.Services.AddSingleton<Services.Interfaces.INotificationService, Services.Implementation.NotificationService>();
        builder.Services.AddSingleton<IOnboardingCoordinator, OnboardingCoordinator>();
        builder.Services.AddSingleton<INutritionAssessmentCoordinator, NutritionAssessmentCoordinator>();
        builder.Services.AddSingleton<IWellnessCheckInCoordinator, WellnessCheckInCoordinator>();
        builder.Services.AddSingleton<ICesAssessmentCoordinator, CesAssessmentCoordinator>();
        builder.Services.AddSingleton<IEquipmentIntelService, EquipmentIntelService>();
        builder.Services.AddSingleton<IExerciseMediaService, WgerExerciseMediaService>();
        builder.Services.AddSingleton<IBundledExerciseMediaService, BundledExerciseMediaService>();
        builder.Services.AddSingleton<IWorkingWeightService, WorkingWeightService>();
        builder.Services.AddSingleton<IExercisePerformanceService, ExercisePerformanceService>();
        // Recipes are now served from the on-device SQLite bundle (Wikibooks
        // Cookbook, seeded on first launch). The MSSQL / API implementations
        // remain in the repo for reference but aren't wired up — the bundle
        // works offline on every platform.
        builder.Services.AddSingleton<IRecipeService, LocalRecipeService>();
        builder.Services.AddSingleton<ISavedRecipeService, SavedRecipeService>();

        // ===== ViewModels =====
        // Auth
        builder.Services.AddTransient<LoginViewModel>();

        // Onboarding wizard
        builder.Services.AddTransient<WelcomeViewModel>();
        builder.Services.AddTransient<PersonalInfoViewModel>();
        builder.Services.AddTransient<BodyMetricsViewModel>();
        builder.Services.AddTransient<HealthScreeningViewModel>();
        builder.Services.AddTransient<TrainingBackgroundViewModel>();
        builder.Services.AddTransient<MovementAssessmentViewModel>();
        builder.Services.AddTransient<FitnessBenchmarkViewModel>();
        builder.Services.AddTransient<GoalsViewModel>();
        builder.Services.AddTransient<NutritionSetupViewModel>();
        builder.Services.AddTransient<SecuritySetupViewModel>();
        builder.Services.AddTransient<ReviewViewModel>();

        // Main app
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddSingleton<AssessmentViewModel>();
        builder.Services.AddTransient<AssessmentResultViewModel>();
        builder.Services.AddTransient<WorkoutProgramViewModel>();
        builder.Services.AddTransient<WorkoutDayViewModel>();

        // Equipment Intel (post-CES)
        builder.Services.AddTransient<EquipmentIntroViewModel>();
        builder.Services.AddTransient<EquipmentDetailViewModel>();
        builder.Services.AddTransient<EnvironmentDetailViewModel>();
        builder.Services.AddTransient<ProgramPreviewViewModel>();
        builder.Services.AddTransient<ExerciseVideoViewModel>();
        builder.Services.AddTransient<WorkingWeightsViewModel>();
        builder.Services.AddTransient<ProgressViewModel>();
        builder.Services.AddTransient<AddProgressEntryViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Nutrition
        builder.Services.AddTransient<NutritionDashboardViewModel>();
        builder.Services.AddTransient<FoodLogViewModel>();
        builder.Services.AddTransient<AddFoodEntryViewModel>();
        builder.Services.AddTransient<BarcodeScannerViewModel>();
        builder.Services.AddTransient<MealPlanViewModel>();
        builder.Services.AddTransient<MealSelectionViewModel>();
        builder.Services.AddTransient<AddCustomFoodViewModel>();

        // Nutrition Assessment
        builder.Services.AddTransient<NutriAssessIntroViewModel>();
        builder.Services.AddTransient<BodyCompositionViewModel>();
        builder.Services.AddTransient<DietPlanViewModel>();
        builder.Services.AddTransient<DietaryHabitsViewModel>();
        builder.Services.AddTransient<BehavioralReadinessViewModel>();
        builder.Services.AddTransient<NutriGoalsViewModel>();
        builder.Services.AddTransient<NutriAssessResultViewModel>();

        // CES Assessment
        builder.Services.AddTransient<CesVm.CesIntroViewModel>();
        builder.Services.AddTransient<CesVm.CesPostureAnteriorViewModel>();
        builder.Services.AddTransient<CesVm.CesPostureLateralViewModel>();
        builder.Services.AddTransient<CesVm.CesPosturePosteriorViewModel>();
        builder.Services.AddTransient<CesVm.CesOverheadSquatViewModel>();
        builder.Services.AddTransient<CesVm.CesSingleLegSquatViewModel>();
        builder.Services.AddTransient<CesVm.CesPushPullViewModel>();
        builder.Services.AddTransient<CesVm.CesResultViewModel>();

        // Wellness Check-In
        builder.Services.AddTransient<WellnessIntroViewModel>();
        builder.Services.AddTransient<WellnessScoffViewModel>();
        builder.Services.AddTransient<WellnessBehavioralViewModel>();
        builder.Services.AddTransient<WellnessResultViewModel>();

        // Goals
        builder.Services.AddTransient<GoalsOverviewViewModel>();
        builder.Services.AddTransient<GoalDetailViewModel>();
        builder.Services.AddTransient<AddGoalViewModel>();

        // Recipes
        builder.Services.AddTransient<RecipeBrowseViewModel>();
        builder.Services.AddTransient<RecipeDetailViewModel>();

        // Schedule
        builder.Services.AddTransient<CalendarViewModel>();
        builder.Services.AddTransient<WorkoutCompleteViewModel>();

        // ===== Pages =====
        // Auth
        builder.Services.AddTransient<LoginPage>();

        // Onboarding
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<PersonalInfoPage>();
        builder.Services.AddTransient<BodyMetricsPage>();
        builder.Services.AddTransient<HealthScreeningPage>();
        builder.Services.AddTransient<TrainingBackgroundPage>();
        builder.Services.AddTransient<MovementAssessmentPage>();
        builder.Services.AddTransient<FitnessBenchmarkPage>();
        builder.Services.AddTransient<GoalsPage>();
        builder.Services.AddTransient<NutritionSetupPage>();
        builder.Services.AddTransient<SecuritySetupPage>();
        builder.Services.AddTransient<ReviewPage>();

        // Main app
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<AssessmentIntroPage>();
        builder.Services.AddTransient<OverheadSquatPage>();
        builder.Services.AddTransient<SingleLegBalancePage>();
        builder.Services.AddTransient<AssessmentResultPage>();
        builder.Services.AddTransient<WorkoutProgramPage>();
        builder.Services.AddTransient<WorkoutDayPage>();

        // Equipment Intel pages
        builder.Services.AddTransient<EquipmentIntroPage>();
        builder.Services.AddTransient<EquipmentDetailPage>();
        builder.Services.AddTransient<EnvironmentDetailPage>();
        builder.Services.AddTransient<ProgramPreviewPage>();
        builder.Services.AddTransient<ExerciseVideoPage>();
        builder.Services.AddTransient<WorkingWeightsPage>();
        builder.Services.AddTransient<ProgressPage>();
        builder.Services.AddTransient<AddProgressEntryPage>();
        builder.Services.AddTransient<SettingsPage>();

        // Nutrition
        builder.Services.AddTransient<NutritionDashboardPage>();
        builder.Services.AddTransient<FoodLogPage>();
        builder.Services.AddTransient<AddFoodEntryPage>();
        builder.Services.AddTransient<BarcodeScannerPage>();
        builder.Services.AddTransient<MealPlanPage>();
        builder.Services.AddTransient<MealSelectionPage>();
        builder.Services.AddTransient<AddCustomFoodPage>();

        // Nutrition Assessment
        builder.Services.AddTransient<NutriAssessIntroPage>();
        builder.Services.AddTransient<BodyCompositionPage>();
        builder.Services.AddTransient<DietPlanPage>();
        builder.Services.AddTransient<DietaryHabitsPage>();
        builder.Services.AddTransient<BehavioralReadinessPage>();
        builder.Services.AddTransient<NutriGoalsPage>();
        builder.Services.AddTransient<NutriAssessResultPage>();

        // CES Assessment
        builder.Services.AddTransient<CesIntroPage>();
        builder.Services.AddTransient<CesPostureAnteriorPage>();
        builder.Services.AddTransient<CesPostureLateralPage>();
        builder.Services.AddTransient<CesPosturePosteriorPage>();
        builder.Services.AddTransient<CesOverheadSquatPage>();
        builder.Services.AddTransient<CesSingleLegSquatPage>();
        builder.Services.AddTransient<CesPushPullPage>();
        builder.Services.AddTransient<CesResultPage>();

        // Wellness Check-In
        builder.Services.AddTransient<WellnessIntroPage>();
        builder.Services.AddTransient<WellnessScoffPage>();
        builder.Services.AddTransient<WellnessBehavioralPage>();
        builder.Services.AddTransient<WellnessResultPage>();

        // Goals
        builder.Services.AddTransient<GoalsOverviewPage>();
        builder.Services.AddTransient<GoalDetailPage>();
        builder.Services.AddTransient<AddGoalPage>();

        // Recipes
        builder.Services.AddTransient<RecipeBrowsePage>();
        builder.Services.AddTransient<RecipeDetailPage>();

        // Schedule
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<WorkoutCompletePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
