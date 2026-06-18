using MAUI_IOT.Pages;

namespace MAUI_IOT
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("sensor", typeof(SensorDetailPage));
        }
    }
}
