using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MAUI_IOT.Models;
using MAUI_IOT.Services;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace MAUI_IOT.PageModels;

public partial class TrendChartModel : ObservableObject
{
    private static readonly SKColor[] Palette =
    [
        new(0x0B, 0x4F, 0xA3),
        new(0x5B, 0x35, 0xB1),
        new(0x08, 0x7A, 0x55),
        new(0xC2, 0x41, 0x0C),
        new(0xB9, 0x1C, 0x1C),
        new(0x0E, 0x74, 0x90),
        new(0xA1, 0x62, 0x07),
        new(0x7C, 0x3A, 0xED),
    ];

    private readonly List<TrendPoint> _points = [];
    private TrendPoint? _selectedPoint;
    private string[] _slotLabels = [];

    public TrendChartModel()
    {
        XAxes = [BuildXAxis(0)];
        YAxes = [BuildYAxis()];
    }

    public ObservableCollection<ISeries> Series { get; } = [];

    [ObservableProperty]
    private Axis[] xAxes = [];

    [ObservableProperty]
    private Axis[] yAxes = [];

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private bool isTemperatureSelected = true;

    public bool IsHumiditySelected => !IsTemperatureSelected;

    public SolidColorPaint LegendTextPaint { get; } = new(SKColor.Parse("#071A3A"));

    public SolidColorPaint TooltipTextPaint { get; } = new(SKColor.Parse("#071A3A"));

    public SolidColorPaint TooltipBackgroundPaint { get; } = new(SKColor.Parse("#FFFFFF"));

    [ObservableProperty]
    private bool hasSelection;

    [ObservableProperty]
    private string selectedDeviceName = string.Empty;

    [ObservableProperty]
    private string selectedValueText = string.Empty;

    [ObservableProperty]
    private string selectedTimeText = string.Empty;

    [ObservableProperty]
    private string selectedLocationText = string.Empty;

    [ObservableProperty]
    private int selectedSensorId;

    public void Load(IReadOnlyList<EspReading> readingsOldestFirst, IReadOnlyDictionary<string, string> deviceNames)
    {
        ResetSelection();
        _points.Clear();
        Series.Clear();

        _slotLabels = new string[readingsOldestFirst.Count];
        var pointsByDevice = new Dictionary<string, List<TrendPoint>>();

        for (var index = 0; index < readingsOldestFirst.Count; index++)
        {
            var reading = readingsOldestFirst[index];
            var localTime = reading.RecordedAtUtc.ToLocalTime().DateTime;
            var deviceName = deviceNames.TryGetValue(reading.DeviceId, out var name) && !string.IsNullOrWhiteSpace(name)
                ? name
                : reading.DeviceId;

            _slotLabels[index] = localTime.ToString("HH:mm");

            var point = new TrendPoint
            {
                Index = index,
                Temperature = reading.TemperatureCelsius,
                Humidity = reading.HumidityPercent,
                DeviceId = reading.DeviceId,
                DeviceName = deviceName,
                RecordedAtLocal = localTime,
                Latitude = reading.Latitude,
                Longitude = reading.Longitude,
                SensorId = SensorIdHasher.ForDevice(reading.DeviceId, isTemperature: true),
                Value = SelectValue(reading),
            };

            _points.Add(point);

            if (!pointsByDevice.TryGetValue(reading.DeviceId, out var devicePoints))
            {
                devicePoints = [];
                pointsByDevice[reading.DeviceId] = devicePoints;
            }

            devicePoints.Add(point);
        }

        var colorIndex = 0;
        foreach (var devicePoints in pointsByDevice.Values)
        {
            var color = Palette[colorIndex % Palette.Length];
            colorIndex++;

            Series.Add(new ColumnSeries<TrendPoint>
            {
                Name = devicePoints[0].DeviceName,
                Values = devicePoints,
                Fill = new SolidColorPaint(color),
                Mapping = (point, _) => new Coordinate(point.Index, point.Value),
                IgnoresBarPosition = true,
                MaxBarWidth = 22,
                Padding = 4,
            });
        }

        XAxes = [BuildXAxis(readingsOldestFirst.Count)];
        YAxes = [BuildYAxis()];
        HasData = readingsOldestFirst.Count > 0;
    }

    public void HandleDataPointerDown(IEnumerable<ChartPoint> points)
    {
        var point = points.FirstOrDefault();
        if (point?.Context.DataSource is not TrendPoint trendPoint)
        {
            return;
        }

        _selectedPoint = trendPoint;
        SelectedDeviceName = trendPoint.DeviceName;
        SelectedTimeText = trendPoint.RecordedAtLocal.ToString("dd.MM.yyyy HH:mm");
        SelectedLocationText = trendPoint is { Latitude: not null, Longitude: not null }
            ? $"{trendPoint.Latitude.Value:F5}, {trendPoint.Longitude.Value:F5}"
            : "No location";
        SelectedSensorId = trendPoint.SensorId;
        RefreshSelectedValue();
        HasSelection = true;
    }

    public void ClearSelection() => ResetSelection();

    [RelayCommand]
    private void SelectTemperature()
    {
        IsTemperatureSelected = true;
        ApplyMetric();
    }

    [RelayCommand]
    private void SelectHumidity()
    {
        IsTemperatureSelected = false;
        ApplyMetric();
    }

    [RelayCommand]
    private void DismissSelection() => ResetSelection();

    private void ResetSelection()
    {
        HasSelection = false;
        _selectedPoint = null;
        SelectedDeviceName = string.Empty;
        SelectedValueText = string.Empty;
        SelectedTimeText = string.Empty;
        SelectedLocationText = string.Empty;
        SelectedSensorId = 0;
    }

    [RelayCommand]
    private async Task ViewDeviceAsync()
    {
        if (SelectedSensorId == 0 || Shell.Current is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"sensor?id={SelectedSensorId}");
    }

    partial void OnIsTemperatureSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsHumiditySelected));
    }

    private void ApplyMetric()
    {
        foreach (var point in _points)
        {
            point.Value = SelectValue(point);
        }

        YAxes = [BuildYAxis()];
        RefreshSelectedValue();
    }

    private double SelectValue(EspReading reading) =>
        IsTemperatureSelected ? reading.TemperatureCelsius : reading.HumidityPercent;

    private double SelectValue(TrendPoint point) =>
        IsTemperatureSelected ? point.Temperature : point.Humidity;

    private void RefreshSelectedValue()
    {
        if (_selectedPoint is null)
        {
            return;
        }

        SelectedValueText = IsTemperatureSelected
            ? $"{_selectedPoint.Temperature:F1} °C"
            : $"{_selectedPoint.Humidity:F1} %";
    }

    private Axis BuildXAxis(int slotCount) => new()
    {
        Labeler = value =>
        {
            var index = (int)Math.Round(value);
            return index >= 0 && index < _slotLabels.Length ? _slotLabels[index] : string.Empty;
        },
        MinLimit = slotCount > 0 ? -0.5 : 0,
        MaxLimit = slotCount > 0 ? slotCount - 0.5 : 1,
        MinStep = 1,
        ForceStepToMin = true,
        TextSize = 11,
        LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7890")),
        SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E1E1E1")),
    };

    private Axis BuildYAxis() => new()
    {
        Labeler = value => IsTemperatureSelected ? $"{value:0.#}°C" : $"{value:0.#}%",
        TextSize = 11,
        LabelsPaint = new SolidColorPaint(SKColor.Parse("#6E7890")),
        SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#E1E1E1")),
    };
}

public partial class TrendPoint : ObservableObject
{
    [ObservableProperty]
    private double value;

    public int Index { get; init; }

    public double Temperature { get; init; }

    public double Humidity { get; init; }

    public string DeviceId { get; init; } = string.Empty;

    public string DeviceName { get; init; } = string.Empty;

    public DateTime RecordedAtLocal { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public int SensorId { get; init; }
}
