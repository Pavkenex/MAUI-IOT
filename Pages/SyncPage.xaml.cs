using MAUI_IOT.PageModels;

namespace MAUI_IOT.Pages;

public partial class SyncPage : ContentPage
{
    private readonly SyncPageModel _pageModel;

    public SyncPage(SyncPageModel pageModel)
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
        if (_pageModel.IsSyncing)
        {
            _pageModel.CancelSyncCommand.Execute(null);
        }
    }
}
