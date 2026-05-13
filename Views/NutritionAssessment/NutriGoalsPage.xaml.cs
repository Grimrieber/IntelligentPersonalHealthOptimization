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
                // Load user's current weight for safety warning calculation
                await vm.LoadCurrentWeightAsync();

                // Guard prevents slider clamping events from corrupting the VM value
                // during initialization (before Min/Max bindings resolve).
                _isInitializingSlider = true;
                vm.InitializeWeight();

                // Defer slider value set to next frame so Min/Max bindings are resolved.
                Dispatcher.Dispatch(() =>
                {
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

    /// <summary>
    /// Sync Slider position when the VM updates TargetWeight programmatically.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isInitializingSlider) return;
        if (e.PropertyName == nameof(NutriGoalsViewModel.TargetWeight)
            && sender is NutriGoalsViewModel vm
            && Math.Abs(WeightSlider.Value - vm.TargetWeight) > 0.01)
        {
            WeightSlider.Value = vm.TargetWeight;
        }
    }
}
