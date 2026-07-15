using IntelligentPersonalHealthOptimization.ViewModels;

namespace IntelligentPersonalHealthOptimization.Views.Recipes;

public partial class CookModePage : ContentPage
{
    public CookModePage(CookModeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
