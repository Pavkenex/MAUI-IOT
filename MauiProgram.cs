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

            builder.Services.AddSingleton<IDeviceDataService, MockDeviceDataService>();
            builder.Services.AddTransient<DevicesPageModel>();
            builder.Services.AddTransient<DevicePage>();

            return builder.Build();
        }
    }
}
