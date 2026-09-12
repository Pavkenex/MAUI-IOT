using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }

    protected override bool OnBackButtonPressed() => true;
}
