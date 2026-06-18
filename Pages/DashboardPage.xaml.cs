using MAUI_IOT.PageModels;
namespace MAUI_IOT.Pages;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardPageModel _pageModel;
	public DashboardPage(DashboardPageModel pageModel)
	{
		InitializeComponent();
		_pageModel = pageModel;
		BindingContext = _pageModel;
	}

	protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _pageModel.LoadDashboardCommand.ExecuteAsync(null);
    }
}
