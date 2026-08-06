using CommunityToolkit.Mvvm.ComponentModel;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class ScanPageModel : ObservableObject
{
    private readonly EspAutoSyncService _autoSync;

    public ScanPageModel(EspAutoSyncService autoSync)
    {
        _autoSync = autoSync;
        Devices = _autoSync.Devices;
        _autoSync.PropertyChanged += OnAutoSyncPropertyChanged;
        Devices.CollectionChanged += OnDevicesChanged;
    }

    public ObservableCollection<EspDeviceRow> Devices { get; }

    public bool IsScanning => _autoSync.IsScanning;

    public string BluetoothStatusText => _autoSync.BluetoothStatusText;

    public string StatusMessage => _autoSync.StatusMessage;

    public string? ErrorMessage => _autoSync.ErrorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasDevices => Devices.Count > 0;

    private void OnAutoSyncPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EspAutoSyncService.IsScanning) or nameof(EspAutoSyncService.BluetoothStatusText)
            or nameof(EspAutoSyncService.StatusMessage) or nameof(EspAutoSyncService.ErrorMessage))
        {
            NotifyStateChanged();
        }
    }

    private void OnDevicesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDevices));
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsScanning));
        OnPropertyChanged(nameof(BluetoothStatusText));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasDevices));
    }
}
