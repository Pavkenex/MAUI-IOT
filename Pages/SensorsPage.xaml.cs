using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;


public partial class SensorsPage : ContentPage
{
	private readonly SensorsPageModel _pageModel;
    public SensorsPage(SensorsPageModel pageModel)
	{
        InitializeComponent();
		_pageModel = pageModel;
        BindingContext = _pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _pageModel.LoadSensorsCommand.ExecuteAsync(null);
    }
}
