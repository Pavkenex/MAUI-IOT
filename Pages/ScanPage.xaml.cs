using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;

public partial class ScanPage : ContentPage
{
    private readonly ScanPageModel _pageModel;

    public ScanPage(ScanPageModel pageModel)
    {
        InitializeComponent();
        _pageModel = pageModel;
        BindingContext = _pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _pageModel.LoadStatusCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_pageModel.IsScanning)
        {
            _pageModel.StopScanCommand.Execute(null);
        }
    }
}
