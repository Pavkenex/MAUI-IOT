using MAUI_IOT.PageModels;
namespace MAUI_IOT.Pages;

public partial class SensorDetailPage : ContentPage
{
	private readonly SensorDetailPageModel _pageModel;

	public SensorDetailPage(SensorDetailPageModel pageModel)
	{
		InitializeComponent();
		_pageModel = pageModel;
		BindingContext = _pageModel;
	}
}
