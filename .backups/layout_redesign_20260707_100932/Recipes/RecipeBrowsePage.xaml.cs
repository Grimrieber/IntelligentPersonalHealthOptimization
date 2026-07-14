using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Recipes;

public partial class RecipeBrowsePage : ContentPage
{
    private readonly RecipeBrowseViewModel _vm;

    public RecipeBrowsePage(RecipeBrowseViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            if (_vm.Categories.Count == 0)
                await _vm.LoadCategoriesCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recipe Load Error", ex.ToString(), "OK");
        }
    }
}
