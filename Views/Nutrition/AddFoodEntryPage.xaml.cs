using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class AddFoodEntryPage : ContentPage
{
    public AddFoodEntryPage(AddFoodEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
