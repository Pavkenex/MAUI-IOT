using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterPageModel pageModel)
    {
        InitializeComponent();
        BindingContext = pageModel;
    }
}
