using IntelligentPersonalHealthOptimization.ViewModels.Onboarding;

namespace IntelligentPersonalHealthOptimization.Views.Onboarding;

public partial class PersonalInfoPage : ContentPage
{
    public PersonalInfoPage(PersonalInfoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
