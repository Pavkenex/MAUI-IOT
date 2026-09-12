using LiveChartsCore.SkiaSharpView.Maui;
using Microsoft.Extensions.Logging;
using MAUI_IOT.PageModels;
using MAUI_IOT.Pages;
using MAUI_IOT.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace MAUI_IOT
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseSkiaSharp()
                .UseLiveCharts()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Services
            builder.Services.AddSingleton<ISensorDataService, SensorDataService>();
            builder.Services.AddSingleton<IEspReadingRepository, EspReadingRepository>();
            builder.Services.AddSingleton<IEspBluetoothService, EspBluetoothService>();
            builder.Services.AddSingleton<IDeviceAuthorizationService, AllowAllDeviceAuthorizationService>();
            builder.Services.AddSingleton<IDeviceLocationService, DeviceLocationService>();
            builder.Services.AddSingleton<EspAutoSyncService>();
            builder.Services.AddSingleton(new HttpClient
            {
                BaseAddress = new Uri(ApiConfig.BaseAddress),
                Timeout = TimeSpan.FromSeconds(30),
            });
            builder.Services.AddSingleton<IApiClient, ApiClient>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<ReadingUploadService>();

            // Page models
            builder.Services.AddTransient<DashboardPageModel>();
            builder.Services.AddTransient<ScanPageModel>();
            builder.Services.AddTransient<SensorsPageModel>();
            builder.Services.AddTransient<SensorDetailPageModel>();
            builder.Services.AddTransient<HistoryPageModel>();
            builder.Services.AddTransient<LoginPageModel>();
            builder.Services.AddTransient<RegisterPageModel>();

            // Pages
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<ScanPage>();
            builder.Services.AddTransient<SensorsPage>();
            builder.Services.AddTransient<SensorDetailPage>();
            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();

            return builder.Build();
        }
    }
}
