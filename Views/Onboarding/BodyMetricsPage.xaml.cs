using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class BodyMetricsPage : ContentPage
{
    public BodyMetricsPage(BodyMetricsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
