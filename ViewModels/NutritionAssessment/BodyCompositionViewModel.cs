using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

public partial class BodyCompositionViewModel : BaseViewModel
{
    private readonly INutritionAssessmentCoordinator _coordinator;
    private const double CmPerInch = 2.54;

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
        SyncToCoordinator();
        await _coordinator.GoNextAsync();
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
