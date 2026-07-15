using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;
using Microsoft.Maui.Graphics;

namespace IntelligentPersonalHealthOptimization.ViewModels;

/// <summary>
/// Last-7-days nutrition trend: logged calories per day as a bar chart vs the
/// user's target, plus weekly averages and days-on-target. Turns food logging
/// (incl. "Log to Today" from recipes) into feedback.
/// </summary>
public partial class NutritionTrendsViewModel : BaseViewModel
{
    private readonly IFoodService _foodService;
    private readonly IUserService _userService;
    private readonly INutritionService _nutritionService;

    private const double ChartHeight = 140;

    public NutritionTrendsViewModel(
        IFoodService foodService, IUserService userService, INutritionService nutritionService)
    {
        _foodService = foodService;
        _userService = userService;
        _nutritionService = nutritionService;
        Title = "Nutrition Trends";
    }

    [ObservableProperty]
    private ObservableCollection<TrendDay> _days = new();

    [ObservableProperty]
    private int _targetCalories;

    [ObservableProperty]
    private string _avgCalories = "0";

    [ObservableProperty]
    private string _avgProtein = "0g";

    [ObservableProperty]
    private string _onTargetSummary = string.Empty;

    [ObservableProperty]
    private bool _hasData;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) { HasData = false; return; }

            var profile = await _nutritionService.GetNutritionProfileAsync(user.Id);
            TargetCalories = profile?.TargetCalories > 0 ? profile.TargetCalories : 2000;

            // Pull the last 7 days (oldest → today).
            var raw = new List<(DateTime date, double cal, double pro)>();
            // Scale bars to the tallest LOGGED day (not the target) so the chart is
            // always readable — even early on with one small day. Colour encodes the
            // target relationship instead. Floor at target/2 so a near-target day
            // doesn't look maxed once real full days are logged.
            double maxCal = 1;
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var t = await _foodService.GetDailyTotalsAsync(user.Id, date);
                raw.Add((date, t.calories, t.proteinG));
                if (t.calories > maxCal) maxCal = t.calories;
            }
            maxCal = Math.Max(maxCal, TargetCalories * 0.5);

            var target = (double)TargetCalories;
            var green = Color.FromArgb("#1F8A4C");
            var primary = Color.FromArgb("#6C4FD8"); // app Primary (purple)

            var days = new ObservableCollection<TrendDay>();
            int onTargetDays = 0, loggedDays = 0;
            double sumCal = 0, sumPro = 0;
            foreach (var (date, cal, pro) in raw)
            {
                bool isToday = date == DateTime.Today;
                bool onTarget = cal > 0 && cal >= target * 0.85 && cal <= target * 1.10;
                if (cal > 0) { loggedDays++; sumCal += cal; sumPro += pro; }
                if (onTarget) onTargetDays++;

                days.Add(new TrendDay
                {
                    DayLabel = date.ToString("ddd"),
                    Calories = (int)Math.Round(cal),
                    ProteinG = (int)Math.Round(pro),
                    // Min 10px so any logged day is clearly visible; 0 for empty days.
                    BarHeight = cal <= 0 ? 0 : Math.Max(10, ChartHeight * cal / maxCal),
                    IsToday = isToday,
                    BarColor = onTarget ? green : primary,
                });
            }

            Days = days;
            HasData = loggedDays > 0;
            AvgCalories = loggedDays > 0 ? $"{(int)Math.Round(sumCal / loggedDays)}" : "0";
            AvgProtein = loggedDays > 0 ? $"{(int)Math.Round(sumPro / loggedDays)}g" : "0g";
            OnTargetSummary = $"{onTargetDays} of 7 days on target";
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutritionTrends load", ex);
            HasData = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>One day's bar in the trend chart.</summary>
public class TrendDay
{
    public string DayLabel { get; set; } = string.Empty;
    public int Calories { get; set; }
    public int ProteinG { get; set; }
    public double BarHeight { get; set; }
    public bool IsToday { get; set; }
    public Color BarColor { get; set; } = Colors.Gray;
}
