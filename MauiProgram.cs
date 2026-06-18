using Microsoft.Extensions.Logging;
using MAUI_IOT.PageModels;
using MAUI_IOT.Pages;
using MAUI_IOT.Services;
namespace MAUI_IOT
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            builder.Services.AddSingleton<ISensorDataService, MockSensorDataService>();
            builder.Services.AddSingleton<IBluetoothSyncService, MockBluetoothSyncService>();
            builder.Services.AddTransient<SensorsPageModel>();
            builder.Services.AddTransient<SensorDetailPageModel>();
            builder.Services.AddTransient<SyncPageModel>();
            builder.Services.AddTransient<SensorsPage>();
            builder.Services.AddTransient<SensorDetailPage>();
            builder.Services.AddTransient<SyncPage>();
            builder.Services.AddTransient<DashboardPageModel>();
            builder.Services.AddTransient<DashboardPage>();

            return builder.Build();
        }
    }
}
