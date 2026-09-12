using MAUI_IOT.Pages;
using MAUI_IOT.Services;

namespace MAUI_IOT
{
    public partial class AppShell : Shell
    {
        private readonly IAuthService _auth;
        private bool _gated;

        public AppShell(IAuthService auth)
        {
            _auth = auth;
            InitializeComponent();
            Routing.RegisterRoute("sensor", typeof(SensorDetailPage));
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("register", typeof(RegisterPage));
            Loaded += OnShellLoaded;
        }

        private async void OnShellLoaded(object? sender, EventArgs e)
        {
            if (_gated)
            {
                return;
            }

            _gated = true;
            await _auth.InitializeAsync();

            if (_auth.HasValidSession)
            {
                return;
            }

            await Task.Yield();
            try
            {
                await GoToAsync("login");
            }
            catch
            {
            }
        }
    }
}
