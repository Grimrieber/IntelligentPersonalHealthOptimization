using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class MealSelectionPage : ContentPage
{
    public MealSelectionPage(MealSelectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
