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

            // Refresh the "Jump back in" strip each time (e.g. after viewing a recipe).
            _vm.RefreshRecentlyViewed();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recipe Load Error", ex.ToString(), "OK");
        }
    }
}
