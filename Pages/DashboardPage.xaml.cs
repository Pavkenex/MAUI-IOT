using LiveChartsCore.Kernel;
using LiveChartsCore.Kernel.Sketches;
using MAUI_IOT.PageModels;
using MAUI_IOT.Services;
namespace MAUI_IOT.Pages;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardPageModel _pageModel;
	private readonly EspAutoSyncService _autoSyncService;

	public DashboardPage(DashboardPageModel pageModel, EspAutoSyncService autoSyncService)
	{
		InitializeComponent();
		_pageModel = pageModel;
		_autoSyncService = autoSyncService;
		BindingContext = _pageModel;
	}

	protected override async void OnAppearing()
    {
        base.OnAppearing();
        _autoSyncService.ReadingsUpdated -= OnReadingsUpdated;
        _autoSyncService.ReadingsUpdated += OnReadingsUpdated;
        await _pageModel.LoadDashboardCommand.ExecuteAsync(null);
    }

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_autoSyncService.ReadingsUpdated -= OnReadingsUpdated;
	}

	private void OnReadingsUpdated(object? sender, EventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			await _pageModel.LoadDashboardCommand.ExecuteAsync(null);
		});
	}

	private void OnTrendDataPointerDown(IChartView chart, IEnumerable<ChartPoint> points)
	{
		_pageModel.Trend.HandleDataPointerDown(points);
	}

	private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
	{
		if (!e.Url.StartsWith("app://", StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		e.Cancel = true;

		var sensorId = _pageModel.LatestSensorId;
		const string sensorPrefix = "app://sensor/";
		if (e.Url.StartsWith(sensorPrefix, StringComparison.OrdinalIgnoreCase) &&
			int.TryParse(e.Url[sensorPrefix.Length..], out var parsedSensorId))
		{
			sensorId = parsedSensorId;
		}

		if (sensorId == 0)
		{
			return;
		}

		await Shell.Current.GoToAsync($"sensor?id={sensorId}");
	}
}
