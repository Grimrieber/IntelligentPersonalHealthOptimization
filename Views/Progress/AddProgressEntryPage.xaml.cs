using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Progress;

public partial class AddProgressEntryPage : ContentPage
{
    public AddProgressEntryPage(AddProgressEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AddProgressEntryViewModel vm)
            await vm.LoadCurrentCommand.ExecuteAsync(null);
    }
}
