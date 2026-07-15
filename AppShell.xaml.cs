using IntelligentPersonalHealthOptimization.Views;
using IntelligentPersonalHealthOptimization.Views.Assessment;
using IntelligentPersonalHealthOptimization.Views.Goals;
using IntelligentPersonalHealthOptimization.Views.Nutrition;
using IntelligentPersonalHealthOptimization.Views.NutritionAssessment;
using IntelligentPersonalHealthOptimization.Views.Onboarding;
using IntelligentPersonalHealthOptimization.Views.Progress;
using IntelligentPersonalHealthOptimization.Views.Recipes;
using IntelligentPersonalHealthOptimization.Views.Schedule;
using IntelligentPersonalHealthOptimization.Views.CesAssessment;
using IntelligentPersonalHealthOptimization.Views.WellnessCheckIn;
using IntelligentPersonalHealthOptimization.Views.Workout;

namespace IntelligentPersonalHealthOptimization;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // When the app resumes, Shell may try to show the login page.
        // Intercept and redirect to Dashboard if session is still valid.
        Navigating += (s, e) =>
        {
            // Only intercept if navigating TO login and session is valid
            if (e.Target?.Location?.OriginalString?.Contains("login") == true && App.HasValidSession)
            {
                e.Cancel();
                App.TouchSession();
                Dispatcher.Dispatch(async () =>
                {
                    await GoToAsync($"//{Constants.RouteConstants.Dashboard}");
                });
            }
        };

        // Onboarding wizard routes
        Routing.RegisterRoute("onboarding/welcome", typeof(WelcomePage));
        Routing.RegisterRoute("onboarding/personalinfo", typeof(PersonalInfoPage));
        Routing.RegisterRoute("onboarding/bodymetrics", typeof(BodyMetricsPage));
        Routing.RegisterRoute("onboarding/healthscreening", typeof(HealthScreeningPage));
        Routing.RegisterRoute("onboarding/trainingbackground", typeof(TrainingBackgroundPage));
        Routing.RegisterRoute("onboarding/movementassessment", typeof(MovementAssessmentPage));
        Routing.RegisterRoute("onboarding/fitnessbenchmark", typeof(FitnessBenchmarkPage));
        Routing.RegisterRoute("onboarding/goals", typeof(GoalsPage));
        Routing.RegisterRoute("onboarding/nutrition", typeof(NutritionSetupPage));
        Routing.RegisterRoute("onboarding/security", typeof(SecuritySetupPage));
        Routing.RegisterRoute("onboarding/review", typeof(ReviewPage));

        // Assessment routes
        Routing.RegisterRoute("assessmentintro", typeof(AssessmentIntroPage));
        Routing.RegisterRoute("overheadsquat", typeof(OverheadSquatPage));
        Routing.RegisterRoute("singlelegbalance", typeof(SingleLegBalancePage));
        Routing.RegisterRoute("assessmentresult", typeof(AssessmentResultPage));

        // Workout routes
        Routing.RegisterRoute("workoutday", typeof(WorkoutDayPage));
        Routing.RegisterRoute("workoutcomplete", typeof(WorkoutCompletePage));
        Routing.RegisterRoute("exercisevideo", typeof(ExerciseVideoPage));
        Routing.RegisterRoute("workingweights", typeof(WorkingWeightsPage));

        // Equipment & Environment intel routes (post-CES, pre-program)
        Routing.RegisterRoute("equipmentintro", typeof(EquipmentIntroPage));
        Routing.RegisterRoute("equipmentdetail", typeof(EquipmentDetailPage));
        Routing.RegisterRoute("environmentdetail", typeof(EnvironmentDetailPage));
        Routing.RegisterRoute("programpreview", typeof(ProgramPreviewPage));

        // Nutrition routes
        Routing.RegisterRoute("foodlog", typeof(FoodLogPage));
        Routing.RegisterRoute("addfoodentry", typeof(AddFoodEntryPage));
        Routing.RegisterRoute("barcodescanner", typeof(BarcodeScannerPage));
        Routing.RegisterRoute("mealplanview", typeof(MealPlanPage));
        Routing.RegisterRoute("mealselection", typeof(MealSelectionPage));
        Routing.RegisterRoute("addcustomfood", typeof(AddCustomFoodPage));

        // Nutrition Assessment routes
        Routing.RegisterRoute("nutriassessintro", typeof(NutriAssessIntroPage));
        Routing.RegisterRoute("nutriassessbodycomp", typeof(BodyCompositionPage));
        Routing.RegisterRoute("nutriassessdietplan", typeof(DietPlanPage));
        Routing.RegisterRoute("nutriassessdietaryhabits", typeof(DietaryHabitsPage));
        Routing.RegisterRoute("nutriassessbehavior", typeof(BehavioralReadinessPage));
        Routing.RegisterRoute("nutriassessgoals", typeof(NutriGoalsPage));
        Routing.RegisterRoute("nutriassessresult", typeof(NutriAssessResultPage));

        // CES Assessment routes
        Routing.RegisterRoute("cesintro", typeof(CesIntroPage));
        Routing.RegisterRoute("cespostureanterior", typeof(CesPostureAnteriorPage));
        Routing.RegisterRoute("cesposturelateral", typeof(CesPostureLateralPage));
        Routing.RegisterRoute("cespostureposterior", typeof(CesPosturePosteriorPage));
        Routing.RegisterRoute("cesoverheadsquat", typeof(CesOverheadSquatPage));
        Routing.RegisterRoute("cessinglelegsquat", typeof(CesSingleLegSquatPage));
        Routing.RegisterRoute("cespushpull", typeof(CesPushPullPage));
        Routing.RegisterRoute("cesresult", typeof(CesResultPage));

        // Wellness Check-In routes
        Routing.RegisterRoute("wellnessintro", typeof(WellnessIntroPage));
        Routing.RegisterRoute("wellnessscoff", typeof(WellnessScoffPage));
        Routing.RegisterRoute("wellnessbehavioral", typeof(WellnessBehavioralPage));
        Routing.RegisterRoute("wellnessresult", typeof(WellnessResultPage));

        // Progress / Goals routes
        Routing.RegisterRoute("addprogress", typeof(AddProgressEntryPage));
        Routing.RegisterRoute("goalsoverview", typeof(GoalsOverviewPage));
        Routing.RegisterRoute("goaldetail", typeof(GoalDetailPage));
        Routing.RegisterRoute("addgoal", typeof(AddGoalPage));

        // Recipe routes
        Routing.RegisterRoute("RecipeDetail", typeof(RecipeDetailPage));
        Routing.RegisterRoute("shoppinglist", typeof(Views.Nutrition.ShoppingListPage));
        Routing.RegisterRoute("cookmode", typeof(Views.Recipes.CookModePage));
        Routing.RegisterRoute("nutritiontrends", typeof(Views.Nutrition.NutritionTrendsPage));
        Routing.RegisterRoute("edittargets", typeof(Views.Nutrition.EditTargetsPage));

        // Schedule routes
        Routing.RegisterRoute("calendar", typeof(CalendarPage));
    }
}
