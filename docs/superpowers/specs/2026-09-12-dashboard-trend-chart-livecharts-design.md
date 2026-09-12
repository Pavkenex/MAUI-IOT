# Design: Dashboard Trend Chart with LiveCharts2 (multi-device)

Date: 2026-09-12
Status: Approved

## Goal

Replace the hand-rolled `BoxView` trend chart on the Dashboard with a real chart rendered by LiveCharts2. The chart shows the **last 7 readings overall, across all ESP devices**, as bars colored per device, with a Temperature/Humidity toggle, a tap tooltip, and a pinned info card that shows the device name, value, time, and location. The info card links to the device's sensor detail page.

## Background / Constraints

- Current chart is fake: `BoxView` bars in `Pages/DashboardPage.xaml:183-258`, normalized heights in `PageModels/DashboardPageModel.cs:293-329`, hardcoded 7 slots, single device.
- No chart dependency exists today; `MAUI&IOT.csproj:69-76` references CommunityToolkit.Mvvm, Microsoft.Maui.Controls, Microsoft.Extensions.Logging.Debug, Plugin.BLE, sqlite-net-pcl, and SQLitePCLRaw.bundle_green — but no chart library and no direct SkiaSharp reference.
- App is .NET 10 MAUI (`net10.0-android`, `-ios`, `-maccatalyst`, `-windows10.0.19041.0`), `MauiXamlInflator=SourceGen`.
- Chosen package: `LiveChartsCore.SkiaSharpView.Maui` **2.0.5** (MIT). Its nuspec ships `net10.0`, `net10.0-android36.0`, `net10.0-ios26.0`, `net10.0-maccatalyst26.0`, `net10.0-windows10.0.19041` targets, so it is compatible.
- LiveCharts2 2.0.5 APIs confirmed against the package's source: `CartesianChart.Series/XAxes/YAxes`, `LegendPosition`, `TooltipPosition`, `DataPointerDown` event (`ChartPointsHandler`, signature `(IChartView chart, IEnumerable<ChartPoint> points)`), per-series `Mapping` (`Func<TModel, int, Coordinate>`), `ColumnSeries<T>`, `SolidColorPaint`, `Axis.Labeler`, and `Series.GetPrimaryToolTipText` / `GetSecondaryToolTipText` for tooltip text. The `DataPointerDown` handler receives non-generic `ChartPoint`; the bound model is reached via `point.Context.DataSource`. Tooltip position and legend position are set on the chart; `YToolTipLabelFormatter`/`XToolTipLabelFormatter` are **not** part of this version's API.
- On Android/iOS, tooltips show while the finger is down and hide on release, so details must be pinned to be readable.
- Data: `EspReading` (Models/EspReading.cs) has `DeviceId`, `TemperatureCelsius`, `HumidityPercent`, `Latitude/Longitude` (nullable), `RecordedAtUtc`. `IEspReadingRepository.GetReadingsAsync(deviceId: null, limit: 7)` returns the newest readings across all devices; `GetDevicesAsync()` returns device records with names.
- Sensor detail navigation already exists: `Shell.Current.GoToAsync($"sensor?id={sensorId}")` where `sensorId = SensorIdHasher.ForDevice(deviceId, isTemperature: true)` (`Pages/DashboardPage.xaml.cs:62`, `AppShell.xaml.cs:15`).
- No automated test framework exists in the repo; verification is build + manual run.

## Requirements (decided with the user)

1. Show the last 7 readings overall (all devices), ordered oldest → newest, left → right.
2. Bars are colored per device; a bottom legend maps colors to device names.
3. Temperature / Humidity toggle (two buttons); switching animates the bars and updates the Y axis unit.
4. Tapping a bar shows the built-in tooltip (series = device name, value + unit, time as the X axis label) and pins an info card below the chart. Location is shown in the pinned card, not in the built-in tooltip.
5. Info card: device name, value, local time, coordinates or "No location", a **View device** button that navigates to `sensor?id=...`, and a dismiss action.
6. If there are no readings, the card shows a "no readings" message instead of the chart.

## Architecture

### Components

1. **`PageModels/TrendChartModel.cs` (new)** — `ObservableObject` owning all chart state. It does not touch repositories; the page model feeds it data.
   - `Load(IReadOnlyList<EspReading> readingsOldestFirst, IReadOnlyDictionary<string, string> deviceNames)` — groups readings by `DeviceId`, rebuilds series, rebuilds the slot-label array (see below), clears any pinned selection, sets `HasData`. An empty list leaves the chart empty with `HasData = false`.
   - Bindables: `ObservableCollection<ISeries> Series`, `Axis[] XAxes`, `Axis[] YAxes`, `bool HasData`.
   - Metric: `TrendMetric` enum (`Temperature`, `Humidity`), `SelectMetricCommand(TrendMetric)`, `bool IsTemperatureSelected` / `IsHumiditySelected`.
   - Selection: `bool HasSelection`, `SelectedDeviceName`, `SelectedValueText`, `SelectedTimeText`, `SelectedLocationText`, `int SelectedSensorId`, `DismissSelectionCommand`, `ViewDeviceCommand`.
   - `HandleDataPointerDown(IEnumerable<ChartPoint> points)` — takes the first hit, reads `point.Context.DataSource as TrendPoint`, sets the pinned selection. (The tooltip at the same slot may show all series' points; the pinned card intentionally describes the first hit only.)
   - Tooltip text uses the series' built-in primary/secondary tooltip text (series name + value) plus the X axis label (time); coordinates are not part of the built-in tooltip.
   - A fixed color palette (8 colors); each device gets a color by first appearance in the current reading window (stable within a load, may shift when the device set changes); the palette cycles if there are more devices.
2. **`PageModels/DashboardPageModel.cs` (edit)** — remove `TrendBars`, `LoadTrendBars`, `NormalizeHeight`, `TrendBar`, `SelectedTrendRange`, `TrendTemperature`, `TrendHumidity`. Add `public TrendChartModel Trend { get; }` (created in the constructor). Keep the card title/subtitle strings. `LoadStoredDataAsync` loads the last 7 readings overall (reversed to oldest-first) plus the device-name map and calls `Trend.Load(...)`. Its existing zero-readings early return must call `Trend.Load([], names)` (or otherwise clear the chart state) so the empty state shows and no stale selection remains.
3. **`Pages/DashboardPage.xaml` (edit)** — replace the fake grid + `BoxView` bars block (`:165-258`) with:
   - Temperature / Humidity toggle buttons bound to `Trend` commands, with the active one visually highlighted and `IsVisible` bound to `Trend.HasData`.
   - `<lvc:CartesianChart IsVisible="{Binding Trend.HasData}" ...>` bound to `Trend.Series`, `Trend.XAxes`, `Trend.YAxes`, `LegendPosition="Bottom"`, `TooltipPosition="Top"`, wired to `DataPointerDown`. Add `xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.Maui;assembly=LiveChartsCore.SkiaSharpView.Maui"`.
   - A selection info card visible when `Trend.HasSelection`, with the fields listed above.
   - An empty-state label shown when `Trend.HasData` is false (reuse `InvertedBoolConverter`).
4. **`Pages/DashboardPage.xaml.cs` (edit)** — handler `OnTrendDataPointerDown(IChartView chart, IEnumerable<ChartPoint> points)` forwards to `_pageModel.Trend.HandleDataPointerDown(points)`.
5. **`MAUI&IOT.csproj` (edit)** — add the `LiveChartsCore.SkiaSharpView.Maui` 2.0.5 `PackageReference`.
6. **`MauiProgram.cs` (edit)** — chain `.UseSkiaSharp()` (from `SkiaSharp.Views.Maui.Controls.Hosting`) and `.UseLiveCharts()` (from `LiveChartsCore.SkiaSharpView.Maui`) on the builder.

### Chart point model and axes

`TrendPoint` carries one reading's chart data: `Index` (0..6 recency slot), `Value` (currently selected metric, observable so the toggle animates in place), `Temperature`, `Humidity`, `DeviceId`, `DeviceName`, `RecordedAtLocal`, `Latitude`, `Longitude`, `SensorId`.

Each device becomes one `ColumnSeries<TrendPoint>` with:

- `Name = deviceName` (legend + default tooltip label),
- `Fill = new SolidColorPaint(paletteColor)`,
- `Mapping = (point, _) => new Coordinate(point.Index, point.Value)`.

`TrendChartModel` keeps a `string[] slotLabels` array (the `HH:mm` label for each occupied slot), rebuilt on every `Load`; the X axis `Labeler` closes over that array and maps the slot index to its label (`Labeler` receives a `double`, not the model). Y axis labeler appends the current unit (`°C` or `%`).

### Data flow

1. Dashboard appears → `LoadDashboardAsync` → `LoadStoredDataAsync`.
2. `GetReadingsAsync(limit: 7)` (all devices, newest first) → reverse to oldest first; index 0 = oldest, index 6 = newest. If the result is empty, call `Trend.Load([], names)` and stop.
3. `GetDevicesAsync()` → device-id → display-name map (fallback to device id).
4. `Trend.Load(readings, names)` → one series per device → legend shows device names.
5. Metric toggle → each `TrendPoint.Value` is set to temperature or humidity → bars animate; Y axis unit and tooltip unit update.
6. Tap (`DataPointerDown`) → first point → pinned selection populates; the built-in tooltip also appears.
7. **View device** → `Shell.Current.GoToAsync($"sensor?id={SelectedSensorId}")`.
8. Dismiss or any reload → selection cleared.

## Error Handling

- Chart loading runs inside the existing `try/catch` in `LoadDashboardAsync`; failures surface via `ErrorMessage` ("Error loading dashboard: ...").
- No readings → `HasData = false`; chart and toggle hidden (`IsVisible` bindings), empty-state message shown, selection cleared.
- Missing `Latitude`/`Longitude` → "No location" in the info card.
- Missing device name → fall back to the device id.
- More devices than palette colors → colors repeat; legend still differentiates by name.
- Any reload clears the pinned selection so a card cannot describe data that is no longer shown.

## Testing & Verification

- `dotnet build "MAUI&IOT.slnx"` with 0 errors (Windows TFM at minimum; Android build if the workload is available).
- Manual run (Windows and/or Android emulator):
  - 0 devices/readings → empty state; 1 device → single-color bars; 2+ devices → distinct colors and legend entries.
  - Toggle Temperature/Humidity → values, tooltip unit, and Y axis unit change.
  - Tap a bar → tooltip shows device, value, and time; the pinned card additionally shows coordinates; a reading without GPS shows "No location".
  - **View device** → sensor detail page opens for the tapped device.
- No test project is added for this change.

## Out of Scope

- Legend tap-to-toggle device visibility (LiveCharts2 has no built-in legend interaction; a custom legend is a possible follow-up).
- Zoom/pan and time-range selection beyond the fixed last-7 readings.
- Charts on other pages (History, Sensor detail).
- GPU/hardware-accelerated rendering and global LiveCharts theme customization.

## Implementation Notes / Risks

- Verify that `TrendPoint.Value` changes animate the chart via `INotifyPropertyChanged`; LiveCharts observes models through a collection observer, but if the mapped model is not observed, rebuild the affected series values on toggle as a fallback.
- `DataPointerDown` uses `FindingStrategy.Automatic`; the column series default (`CompareOnlyXTakeClosest`) is the intended tap behavior.
- Confirm XAML source generation (`MauiXamlInflator=SourceGen`) builds with the third-party `lvc` namespace; otherwise set `Inflator="Default"` for `DashboardPage.xaml` only.
- The chart needs explicit height (the current card uses a 236px block); keep it inside the existing card so layout does not collapse.
