using MAUI_IOT.Services;

namespace MAUI_IOT
{
    public partial class App : Application
    {
        private readonly EspAutoSyncService _autoSyncService;
        private readonly ReadingUploadService _uploadService;
        private readonly IAuthService _auth;

        public App(
            EspAutoSyncService autoSyncService,
            ReadingUploadService uploadService,
            IAuthService auth)
        {
            InitializeComponent();
            UserAppTheme = AppTheme.Light;
            _autoSyncService = autoSyncService;
            _uploadService = uploadService;
            _auth = auth;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_auth));
        }

        protected override void OnStart()
        {
            StartSafely(_autoSyncService.Start);
            StartSafely(_uploadService.Start);
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