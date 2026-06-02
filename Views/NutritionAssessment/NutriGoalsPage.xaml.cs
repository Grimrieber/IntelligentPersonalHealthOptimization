using IntelligentPersonalHealthOptimization.ViewModels.NutritionAssessment;

namespace IntelligentPersonalHealthOptimization.Views.NutritionAssessment;

public partial class NutriGoalsPage : ContentPage
{
    private bool _isInitializingSlider;

    public NutriGoalsPage(NutriGoalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            if (BindingContext is NutriGoalsViewModel vm)
            {
                // Load user record (used both for initial current weight and to persist edits)
                await vm.LoadCurrentWeightAsync();

                // Guard prevents slider clamping events from corrupting the VM value
                // during initialization (before Min/Max bindings resolve).
                _isInitializingSlider = true;
                vm.InitializeWeight();

                // Defer slider value sets to next frame so Min/Max bindings are resolved.
                Dispatcher.Dispatch(() =>
                {
                    CurrentWeightSlider.Value = vm.CurrentWeight;
                    WeightSlider.Value = vm.TargetWeight;
                    _isInitializingSlider = false;
                });
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("NutriGoalsPage OnAppearing", ex);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as NutriGoalsViewModel)?.SyncToCoordinator();
    }

    private void OnWeightSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isInitializingSlider) return;
        if (BindingContext is NutriGoalsViewModel vm)
            vm.TargetWeight = e.NewValue;
    }

    private void OnCurrentWeightSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_isInitializingSlider) return;
        if (BindingContext is NutriGoalsViewModel vm)
            vm.CurrentWeight = e.NewValue;
    }

    /// <summary>Sync Slider positions when the VM updates the values programmatically.</summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isInitializingSlider) return;
        if (sender is not NutriGoalsViewModel vm) return;

        if (e.PropertyName == nameof(NutriGoalsViewModel.TargetWeight)
            && Math.Abs(WeightSlider.Value - vm.TargetWeight) > 0.01)
        {
            WeightSlider.Value = vm.TargetWeight;
        }
        else if (e.PropertyName == nameof(NutriGoalsViewModel.CurrentWeight)
            && Math.Abs(CurrentWeightSlider.Value - vm.CurrentWeight) > 0.01)
        {
            CurrentWeightSlider.Value = vm.CurrentWeight;
        }
    }
}
