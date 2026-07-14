using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Nutrition;

public partial class AddFoodEntryPage : ContentPage
{
    private readonly AddFoodEntryViewModel _viewModel;

    public AddFoodEntryPage(AddFoodEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadContextCommand.Execute(null);
    }
}
