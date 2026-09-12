using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Services;

namespace MAUI_IOT.PageModels;

public partial class LoginPageModel : ObservableObject
{
    private readonly IAuthService _auth;

    public LoginPageModel(IAuthService auth) => _auth = auth;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your username and password.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var result = await _auth.LoginAsync(Username.Trim(), Password);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Login failed.";
                return;
            }

            Password = string.Empty;
            await Shell.Current.GoToAsync("//dashboard");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToRegisterAsync() => Shell.Current.GoToAsync("register");
}
