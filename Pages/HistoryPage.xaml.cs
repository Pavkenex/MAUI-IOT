using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryPageModel _pageModel;

    public HistoryPage(HistoryPageModel pageModel)
    {
        InitializeComponent();
        _pageModel = pageModel;
        BindingContext = _pageModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _pageModel.LoadHistoryCommand.ExecuteAsync(null);
    }
}
