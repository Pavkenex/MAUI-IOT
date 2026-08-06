using MAUI_IOT.Services;

namespace MAUI_IOT
{
    public partial class App : Application
    {
        private readonly EspAutoSyncService _autoSyncService;

        public App(EspAutoSyncService autoSyncService)
        {
            InitializeComponent();
            _autoSyncService = autoSyncService;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override void OnStart()
        {
            _autoSyncService.Start();
        }
    }
}