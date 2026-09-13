# Design: Sensor Detail Location Map

Date: 2026-09-12
Status: Approved

## Goal

On the Sensor details page, show the sensor's location on a map that updates to whichever reading the user selects from the list below. This makes it easy to see where the phone was when each reading was received.

## Background / Constraints

- The app already renders maps on the Dashboard with a Leaflet 1.9.4 map inside a `WebView`, driven by an `HtmlWebViewSource`. See `PageModels/DashboardPageModel.cs:194-260` (`BuildMapSource`) and `Pages/DashboardPage.xaml:122-150`, with navigation handled in `Pages/DashboardPage.xaml.cs:47-70` (`OnMapNavigating`).
- Location for each reading is captured from the phone when readings are received from the ESP (stored as nullable `Latitude`/`Longitude` on `EspReading`).
- The Sensor details page (`Pages/SensorDetailPage.xaml`) is a `RefreshView` wrapping a `CollectionView`. The list item template (`Pages/SensorDetailPage.xaml:121-147`) already shows a `Location: lat, lon` line (via `Reading.LocationText`) when `Reading.HasLocation`.
- `Reading` model (`Models/Reading.cs`) exposes `Latitude`, `Longitude`, `HasLocation`, `LocationText`, `Value`, and `Timestamp` (local).
- The page model lives in `PageModels/SensorDetailPageModel.cs`; it currently does not hold any selected-reading or map state.
- No automated test framework exists in the repo; verification is build + manual run.

## Requirements (decided with the user)

1. Add a map to the Sensor details page showing the selected reading's location with a single marker.
2. The map reflects the "Selected reading only" — one marker that moves to whichever reading is tapped.
3. Default selection on load is the newest reading that has a location; if no readings have a location, show a placeholder.
4. The map stays pinned at the top while the readings list scrolls beneath it.
5. Tapping a reading updates the map marker, and the tapped card is visually highlighted.
6. Tap of a reading without coordinates clears the marker and shows a placeholder, but the card is still highlighted.

## Architecture

### Components

1. **`PageModels/SensorDetailPageModel.cs` (edit)**
   - New `[ObservableProperty] Reading? selectedReading` — bound to `CollectionView.SelectedItem`.
   - New `[ObservableProperty] HtmlWebViewSource? mapSource`.
   - New computed `bool hasMapLocation` — true only when `SelectedReading` has a location (`IsVisible` binding). Notify via `NotifyPropertyChangedFor` on `SelectedReading`.
   - `partial void OnSelectedReadingChanged(...)` reacts to selection changes and rebuilds the map via `BuildMapView()`.
   - `BuildMapView()` renders a single-leaflet-marker `HtmlWebViewSource`, adapted from the dashboard's `BuildMapSource` but for one point. The popup shows the reading's value, local timestamp, and formatted coordinates. Longitude/latitude formatted with `CultureInfo.InvariantCulture`. Escapes the value text before injecting into HTML.
   - Display order: the readings list shows newest first. `SensorDataService.GetReadingsAsync` returns oldest → newest, so `LoadSensorAsync` inserts the results in reverse; the service contract and its ascending `Reading.Id` assignment stay unchanged.
   - Default selection: after readings populate in `LoadSensorAsync`, pick the newest reading with `HasLocation` (the first match in the newest-first collection) and set `SelectedReading`; if none, leave it null so the placeholder shows.
   - Mounting note: `SelectionMode="Single"` + `SelectedItem` means `Readings.Clear()` during a load/refresh resets the selection and fires `OnSelectedReadingChanged` with null mid-load. `OnSelectedReadingChanged` must null-handle gracefully (it can simply rebuild the empty-map state), and the final newest-with-location reselect must be applied only *after* the list re-populates so a stale empty map does not flash.
2. **`Pages/SensorDetailPage.xaml` (edit)**
   - Root becomes a `Grid` with `RowDefinitions="Auto,*"`:
     - Row 0: a pinned `Border` (height ~200), styled like the dashboard map card, containing the map `WebView` (visible when `HasMapLocation`) and a "No location for this reading" placeholder (visible when not).
     - Row 1: the existing `RefreshView` / `CollectionView`.
   - `CollectionView`: set `SelectionMode="Single"` and `SelectedItem="{Binding SelectedReading}"`. Add a `VisualState` (Selected) in the item template to highlight the selected card (accent border/background).
   - The header keeps the title/info card and the "Recent readings" label.
3. **`Pages/SensorDetailPage.xaml.cs` (edit)** — no logic change required for the map itself; if any `app://` navigation is wired into the single-marker popup a `Navigating` handler would be added, but the popup intentionally has no nav links (we are already on the sensor page), so no code-behind change is expected.

### Data flow

1. Page opens → `ApplyQueryAttributes` → `LoadSensorAsync` → load sensor + readings into `Readings`.
2. If any reading `HasLocation`, set `SelectedReading` to the newest of those → `OnSelectedReadingChanged` → `BuildMapView()` → WebView shows the marker and fits the single point at a fixed zoom.
3. Otherwise `SelectedReading` stays null → placeholder shown.
4. User taps a reading → `CollectionView.SelectedItem` updates `SelectedReading` → map marker moves (or placeholder if tapped reading lacks a location) and the tapped card is highlighted.
5. Pull-to-refresh / Refresh → reload, reselect newest-with-location.

## Error Handling

- No located readings → placeholder in the pinned map band; WebView hidden via `IsVisible`.
- Selected reading without coordinates → marker cleared, placeholder shown, card still highlighted (so the user sees why the map is empty).
- HTML-safe injection: reading value and coordinates are escaped/encoded before being placed in the map HTML.
- Null `MapSource` guarded by `IsVisible` so the WebView never loads with a null source.

## Testing & Verification

- `dotnet build "MAUI&IOT.slnx"` with 0 errors (Windows TFM at minimum; Android if the workload is available).
- Manual run (Windows and/or Android emulator):
  - 0 located readings → placeholder.
  - Default load → marker on the newest located reading.
  - Tap a reading → marker moves to that reading and the card highlights.
  - Tap a no-location reading → placeholder shown, card highlighted.
  - Refresh → reselects newest-with-location.
- No test project is added for this change.

## Out of Scope

- Multiple markers or a movement trail across readings.
- Native map controls / API-key-based providers.
- Device ("View device") navigation from this page's map (already on the sensor page).