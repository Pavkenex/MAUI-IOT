using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;


public partial class DevicePage : ContentPage
{
	private readonly DevicesPageModel _pageModel;
    public DevicePage(DevicesPageModel pageModel)
	{
        InitializeComponent();
		_pageModel = pageModel;
        BindingContext = _pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _pageModel.LoadDevicesCommand.ExecuteAsync(null);
    }
}