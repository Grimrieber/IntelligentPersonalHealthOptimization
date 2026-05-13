using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class AddCustomFoodPage : ContentPage
{
    public AddCustomFoodPage(AddCustomFoodViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
