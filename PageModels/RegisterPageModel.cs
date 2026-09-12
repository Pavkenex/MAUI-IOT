using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_IOT.Services;

namespace MAUI_IOT.PageModels;

public partial class RegisterPageModel : ObservableObject
{
    private readonly IAuthService _auth;

    public RegisterPageModel(IAuthService auth) => _auth = auth;

    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter a username and password.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var result = await _auth.RegisterAsync(Username.Trim(), Password);
            if (!result.Success)
            {
                ErrorMessage = result.Error ?? "Registration failed.";
                return;
            }

            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await Shell.Current.GoToAsync("//dashboard");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task GoToLoginAsync() => Shell.Current.GoToAsync("..");
}
