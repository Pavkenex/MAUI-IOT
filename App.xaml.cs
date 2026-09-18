using MAUI_IOT.Services;

namespace MAUI_IOT
{
    public partial class App : Application
    {
        private readonly EspAutoSyncService _autoSyncService;
        private readonly ReadingUploadService _uploadService;
        private readonly CloudSyncService _cloudSyncService;
        private readonly IAuthService _auth;

        public App(
            EspAutoSyncService autoSyncService,
            ReadingUploadService uploadService,
            CloudSyncService cloudSyncService,
            IAuthService auth)
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Light;
            _autoSyncService = autoSyncService;
            _uploadService = uploadService;
            _cloudSyncService = cloudSyncService;
            _auth = auth;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell(_auth));
#if WINDOWS
            window.Width = 1400;
            window.Height = 900;
            window.MinimumWidth = 760;
            window.MinimumHeight = 560;
#endif
            window.Activated += (_, _) => _cloudSyncService.NotifyAppForegrounded();
            return window;
        }

        protected override void OnStart()
        {
            StartSafely(_autoSyncService.Start);
            StartSafely(_uploadService.Start);
            StartSafely(_cloudSyncService.Start);
        }

        private static void StartSafely(Action start)
        {
            try
            {
                start();
            }
            catch
            {
            }
        }
    }
}