using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class BodyCompositionViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;
    private const double CmPerInch = 2.54;
    private const double MinCm = 10, MaxCm = 250;   // plausible human circumference range

    public BodyCompositionViewModel(INutritionAssessmentCoordinator coordinator)
    {
        _coordinator = coordinator;
        Title = _coordinator.StepTitle;

        // Stored values are always cm; display starts in cm.
        _neckText = FromCm(_coordinator.Data.NeckCm);
        _chestText = FromCm(_coordinator.Data.ChestCm);
        _waistText = FromCm(_coordinator.Data.WaistCm);
        _hipsText = FromCm(_coordinator.Data.HipsCm);
        _thighText = FromCm(_coordinator.Data.ThighCm);
        _calfText = FromCm(_coordinator.Data.CalfCm);
        _bicepText = FromCm(_coordinator.Data.BicepCm);
    }

    public string StepTitle => _coordinator.StepTitle;
    public double ProgressPercentage => _coordinator.ProgressPercentage / 100.0;
    public string StepIndicator => $"Step {_coordinator.CurrentStep + 1} of {_coordinator.TotalSteps}";

    /// <summary>Whether entries are shown/typed in cm (true) or inches (false).
    /// Values are always persisted as cm regardless.</summary>
    [ObservableProperty]
    private bool _unitIsCm = true;

    public string UnitLabel => UnitIsCm ? "cm" : "in";
    public string UnitTitle => $"Circumference Measurements ({UnitLabel})";

    partial void OnUnitIsCmChanged(bool value)
    {
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(UnitTitle));
    }

    // Entry text in the currently-selected unit.
    [ObservableProperty] private string _neckText = string.Empty;
    [ObservableProperty] private string _chestText = string.Empty;
    [ObservableProperty] private string _waistText = string.Empty;
    [ObservableProperty] private string _hipsText = string.Empty;
    [ObservableProperty] private string _thighText = string.Empty;
    [ObservableProperty] private string _calfText = string.Empty;
    [ObservableProperty] private string _bicepText = string.Empty;

    /// <summary>Switch the displayed unit, converting any entered values in place.</summary>
    [RelayCommand]
    private void SetUnit(string unit)
    {
        var toCm = string.Equals(unit, "cm", StringComparison.OrdinalIgnoreCase);
        if (toCm == UnitIsCm) return;

        NeckText = ConvertUnit(NeckText, toCm);
        ChestText = ConvertUnit(ChestText, toCm);
        WaistText = ConvertUnit(WaistText, toCm);
        HipsText = ConvertUnit(HipsText, toCm);
        ThighText = ConvertUnit(ThighText, toCm);
        CalfText = ConvertUnit(CalfText, toCm);
        BicepText = ConvertUnit(BicepText, toCm);

        UnitIsCm = toCm;
    }

    public void SyncToCoordinator()
    {
        _coordinator.Data.NeckCm = ToCm(NeckText);
        _coordinator.Data.ChestCm = ToCm(ChestText);
        _coordinator.Data.WaistCm = ToCm(WaistText);
        _coordinator.Data.HipsCm = ToCm(HipsText);
        _coordinator.Data.ThighCm = ToCm(ThighText);
        _coordinator.Data.CalfCm = ToCm(CalfText);
        _coordinator.Data.BicepCm = ToCm(BicepText);
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        // Measurements are optional, but reject implausible/garbage entries so a
        // typo like "999" doesn't silently pollute the assessment.
        if (!TryValidate(out var error))
        {
            await Shell.Current.DisplayAlert("Check your measurements", error, "OK");
            return;
        }

        SyncToCoordinator();
        await _coordinator.GoNextAsync();
    }

    private bool TryValidate(out string error)
    {
        var fields = new (string Name, string Text)[]
        {
            ("Neck", NeckText), ("Chest", ChestText), ("Waist", WaistText),
            ("Hips", HipsText), ("Thigh", ThighText), ("Calf", CalfText), ("Bicep", BicepText),
        };

        foreach (var (name, text) in fields)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;   // optional — blank is fine

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0)
            {
                error = $"{name}: please enter a number (or leave it blank).";
                return false;
            }

            var cm = UnitIsCm ? v : v * CmPerInch;
            if (cm < MinCm || cm > MaxCm)
            {
                var lo = UnitIsCm ? MinCm : MinCm / CmPerInch;
                var hi = UnitIsCm ? MaxCm : MaxCm / CmPerInch;
                error = $"{name}: {v:0.#} {UnitLabel} is outside the expected range " +
                        $"({lo:0} – {hi:0} {UnitLabel}). Please double-check it.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    [RelayCommand]
    private async Task PreviousAsync()
    {
        SyncToCoordinator();
        await _coordinator.GoPreviousAsync();
    }

    // --- unit helpers --------------------------------------------------------

    /// <summary>Format a stored cm value into the current display unit.</summary>
    private string FromCm(double cm)
    {
        if (cm <= 0) return string.Empty;
        var v = UnitIsCm ? cm : cm / CmPerInch;
        return Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Parse the displayed (current-unit) text back to cm for storage.</summary>
    private double ToCm(string text)
    {
        var v = ParseDouble(text);
        return UnitIsCm ? v : v * CmPerInch;
    }

    /// <summary>Convert a typed value between units when the toggle flips.</summary>
    private static string ConvertUnit(string text, bool toCm)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v <= 0)
            return text;
        var result = toCm ? v * CmPerInch : v / CmPerInch;
        return Math.Round(result, 1).ToString(CultureInfo.InvariantCulture);
    }

    private static double ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : 0;
}
