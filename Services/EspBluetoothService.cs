using MAUI_IOT.Models;
using MAUI_IOT.Protocol;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;

namespace MAUI_IOT.Services;

public sealed class EspBluetoothService : IEspBluetoothService
{
    private static readonly TimeSpan DefaultScanTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CharacteristicReadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SynchronizationTimeout = TimeSpan.FromSeconds(120);

    private readonly IAdapter _adapter;
    private readonly IEspReadingRepository _repository;
    private readonly IDeviceLocationService _locationService;
    private readonly Dictionary<string, EspDeviceInfo> _scannedDevices = new();
    private readonly Dictionary<string, IDevice> _bleDevices = new();
    private readonly Dictionary<string, ICharacteristic> _characteristics = new();
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    private readonly byte[] _serviceUuidBytes = CreateServiceUuidBytes();

    private IDevice? _connectedDevice;
    private EspDeviceInfo? _connectedDeviceInfo;
    private TransferState? _transfer;
    private bool _connectionLostHandlerAttached;

    public event EventHandler? ConnectionLost;

    public EspBluetoothService(
        IEspReadingRepository repository,
        IDeviceLocationService locationService)
    {
        _repository = repository;
        _locationService = locationService;
        _adapter = CrossBluetoothLE.Current.Adapter;
        _adapter.DeviceConnectionLost += OnDeviceConnectionLost;
        _connectionLostHandlerAttached = true;
    }

    public bool IsConnected => _connectedDevice?.State == DeviceState.Connected;

    public bool IsScanning => _adapter.IsScanning;

    public EspDeviceInfo? ConnectedDevice => _connectedDeviceInfo;

    public async Task<bool> RequestBluetoothPermissionAsync()
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                return await Permissions.RequestAsync<Permissions.Bluetooth>() == PermissionStatus.Granted;
            }

            if (OperatingSystem.IsAndroid())
            {
                return await Permissions.RequestAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted;
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<bool> IsBluetoothEnabledAsync()
    {
        return Task.FromResult(CrossBluetoothLE.Current.State == BluetoothState.On);
    }

    public async Task<IReadOnlyList<EspDeviceInfo>> ScanForDevicesAsync(
        IProgress<EspDeviceInfo>? progress = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (!await IsBluetoothEnabledAsync())
        {
            throw new EspSyncException("Bluetooth is disabled on this device.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? DefaultScanTimeout);

        if (_adapter.IsScanning)
        {
            await StopScanAsync();
        }

        var found = new Dictionary<string, EspDeviceInfo>();
        _scannedDevices.Clear();

        void OnDiscovered(object? sender, DeviceEventArgs e)
        {
            var device = e.Device;
            if (!IsEspDevice(device))
            {
                return;
            }

            _bleDevices[device.Id.ToString()] = device;
            var info = CreateDeviceInfo(device);
            found[info.Id] = info;
            progress?.Report(info);
        }

        void OnAdvertised(object? sender, DeviceEventArgs e)
        {
            var device = e.Device;
            if (!IsEspDevice(device))
            {
                return;
            }

            if (found.TryGetValue(device.Id.ToString(), out var existing))
            {
                var updated = existing with { Rssi = device.Rssi };
                found[existing.Id] = updated;
                progress?.Report(updated);
            }
        }

        _adapter.DeviceDiscovered += OnDiscovered;
        _adapter.DeviceAdvertised += OnAdvertised;

        try
        {
            await _adapter.StartScanningForDevicesAsync(cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _adapter.DeviceDiscovered -= OnDiscovered;
            _adapter.DeviceAdvertised -= OnAdvertised;
            if (_adapter.IsScanning)
            {
                try
                {
                    await _adapter.StopScanningForDevicesAsync();
                }
                catch (Exception)
                {
                }
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("Scan cancelled.", cancellationToken);
        }

        var results = found.Values.OrderByDescending(d => d.Rssi).ToList();
        foreach (var result in results)
        {
            _scannedDevices[result.Id] = result;
        }

        return results;
    }

    public Task StopScanAsync()
    {
        if (!_adapter.IsScanning)
        {
            return Task.CompletedTask;
        }

        return _adapter.StopScanningForDevicesAsync();
    }

    public EspDeviceInfo? GetScannedDevice(string id)
    {
        return _scannedDevices.TryGetValue(id, out var device) ? device : null;
    }

    public async Task ConnectAsync(EspDeviceInfo device, CancellationToken cancellationToken = default)
    {
        if (IsConnected && _connectedDeviceInfo?.Id == device.Id)
        {
            return;
        }

        if (_adapter.IsScanning)
        {
            await StopScanAsync();
        }

        await DisconnectAsync();

        if (!_bleDevices.TryGetValue(device.Id, out var bleDevice))
        {
            throw new EspSyncException("Device is no longer available. Run a scan first.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ConnectTimeout);

        try
        {
            await _adapter.ConnectToDeviceAsync(bleDevice, cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new EspSyncException($"Connection to {device.Name} timed out.");
            }

            throw;
        }
        catch (Exception ex)
        {
            throw new EspSyncException($"Connection to {device.Name} failed: {ex.Message}", ex);
        }

        _connectedDevice = bleDevice;
        _connectedDeviceInfo = device with { IsConnected = true };
    }

    public async Task DisconnectAsync()
    {
        if (_connectedDevice is not null && _connectedDevice.State == DeviceState.Connected)
        {
            try
            {
                await _adapter.DisconnectDeviceAsync(_connectedDevice);
            }
            catch (Exception)
            {
            }
        }

        _connectedDevice = null;
        _connectedDeviceInfo = null;
        _characteristics.Clear();
    }

    public async Task<EspSyncResult> SynchronizeAsync(
        EspDeviceInfo device,
        EspDeviceIdentity identity,
        IProgress<EspSyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _syncGate.WaitAsync(0))
        {
            throw new EspSyncException("Synchronization is already in progress.");
        }

        try
        {
            return await RunSynchronizationAsync(device, identity, progress, cancellationToken);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task<EspDeviceIdentity> ReadDeviceIdentityAsync(
        EspDeviceInfo device,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _connectedDevice is null)
        {
            throw new EspSyncException("Device is not connected.");
        }

        if (_connectedDeviceInfo?.Id != device.Id)
        {
            throw new EspSyncException($"Connected to a different device ({_connectedDeviceInfo?.Name ?? "unknown"}).");
        }

        var identityCharacteristic = await GetCharacteristicAsync(
            _connectedDevice.Id.ToString(),
            EspProtocol.IdentityUuid,
            cancellationToken);

        return await ReadIdentityAsync(identityCharacteristic, cancellationToken);
    }

    private async Task<EspSyncResult> RunSynchronizationAsync(
        EspDeviceInfo device,
        EspDeviceIdentity identity,
        IProgress<EspSyncProgress>? progress,
        CancellationToken cancellationToken)
    {
        Report(progress, new EspSyncProgress(EspSyncStage.Connecting, 0, 0, 0, 0));
        await ConnectAsync(device, cancellationToken);

        if (_connectedDevice is null)
        {
            throw new EspSyncException("Device is not connected.");
        }

        var deviceIdKey = _connectedDevice.Id.ToString();
        var uptimeCharacteristic = await GetCharacteristicAsync(deviceIdKey, EspProtocol.UptimeUuid, cancellationToken);
        var rangeCharacteristic = await GetCharacteristicAsync(deviceIdKey, EspProtocol.RangeUuid, cancellationToken);
        var controlCharacteristic = await GetCharacteristicAsync(deviceIdKey, EspProtocol.ControlUuid, cancellationToken);
        var dataCharacteristic = await GetCharacteristicAsync(deviceIdKey, EspProtocol.DataUuid, cancellationToken);

        Report(progress, new EspSyncProgress(EspSyncStage.ReadingDeviceInfo, 0, 0, 0, 0));

        var range = await ReadRangeAsync(rangeCharacteristic, cancellationToken);
        var phoneLocation = await _locationService.GetCurrentLocationAsync(cancellationToken);

        var transfer = await StartTransferAsync(
            identity,
            range,
            phoneLocation,
            uptimeCharacteristic,
            controlCharacteristic,
            dataCharacteristic,
            progress,
            cancellationToken,
            allowSessionRetry: true);

        return transfer;
    }

    private async Task<EspSyncResult> StartTransferAsync(
        EspDeviceIdentity identity,
        EspReadingRange range,
        Location? phoneLocation,
        ICharacteristic uptimeCharacteristic,
        ICharacteristic controlCharacteristic,
        ICharacteristic dataCharacteristic,
        IProgress<EspSyncProgress>? progress,
        CancellationToken cancellationToken,
        bool allowSessionRetry)
    {
        var cursor = await _repository.GetCursorAsync(identity.DeviceId);
        var isNewSession = cursor is null || cursor.BootSessionId != identity.BootSessionId;
        var requestedAfterId = isNewSession ? 0u : cursor!.LastPersistedReadingId;

        var hasGapFromCursor = !isNewSession && requestedAfterId > 0 && requestedAfterId < range.OldestReadingId;

        var (uptime, phoneAnchorUtc) = await ReadUptimeAsync(uptimeCharacteristic, cancellationToken);

        Report(progress, new EspSyncProgress(EspSyncStage.Subscribing, 0, 0, 0, 0));

        var transfer = new TransferState(
            identity.DeviceId,
            identity.BootSessionId,
            uptime,
            phoneAnchorUtc,
            phoneLocation,
            requestedAfterId,
            hasGapFromCursor,
            progress);

        _transfer = transfer;

        void OnDataUpdated(object? sender, CharacteristicUpdatedEventArgs e)
        {
            HandleDataNotification(transfer, e.Characteristic.Value);
        }

        using var syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        syncCts.CancelAfter(SynchronizationTimeout);

        var subscribed = false;
        var retriedSession = false;
        try
        {
            dataCharacteristic.ValueUpdated += OnDataUpdated;
            await dataCharacteristic.StartUpdatesAsync(syncCts.Token);
            subscribed = true;

            var requestPacket = EspControlEncoder.RequestAfter(identity.BootSessionId, requestedAfterId);
            var writeResult = await controlCharacteristic.WriteAsync(requestPacket, syncCts.Token);
            if (writeResult != 0)
            {
                throw new EspSyncException($"Writing REQUEST_AFTER failed with BLE result code {writeResult}.");
            }

            Report(progress, new EspSyncProgress(EspSyncStage.Transferring, 0, 0, 0, 0));

            var outcome = await transfer.Completion.WaitAsync(syncCts.Token);

            if (outcome.Kind == TransferOutcomeKind.SessionMismatch && allowSessionRetry)
            {
                retriedSession = true;
                _transfer = null;
                await SendCancelAsync(controlCharacteristic, cancellationToken);

                var newIdentity = await ReadIdentityAsync(
                    await GetCharacteristicAsync(_connectedDevice!.Id.ToString(), EspProtocol.IdentityUuid, cancellationToken),
                    cancellationToken);
                var newRange = await ReadRangeAsync(
                    await GetCharacteristicAsync(_connectedDevice.Id.ToString(), EspProtocol.RangeUuid, cancellationToken),
                    cancellationToken);

                var result = await StartTransferAsync(
                    newIdentity,
                    newRange,
                    phoneLocation,
                    uptimeCharacteristic,
                    controlCharacteristic,
                    dataCharacteristic,
                    progress,
                    cancellationToken,
                    allowSessionRetry: false);
                return result with { HasDataGap = result.HasDataGap || outcome.HasGap };
            }

            if (outcome.Kind == TransferOutcomeKind.InvalidCommand)
            {
                throw new EspSyncException("The ESP rejected the command (INVALID_COMMAND).");
            }

            if (outcome.Kind == TransferOutcomeKind.Disconnected)
            {
                throw new EspSyncException("Connection to the ESP was lost during synchronization.");
            }

            if (outcome.Kind == TransferOutcomeKind.Cancelled)
            {
                throw new OperationCanceledException("Synchronization cancelled.", cancellationToken);
            }

            if (outcome.Kind == TransferOutcomeKind.Timeout)
            {
                throw new EspSyncException("Synchronization timed out.");
            }

            if (transfer.LastPersistedReadingId > 0)
            {
                await _repository.SaveCursorAsync(new EspSyncCursor
                {
                    DeviceId = transfer.DeviceId,
                    BootSessionId = transfer.BootSessionId,
                    LastPersistedReadingId = transfer.LastPersistedReadingId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
            }

            Report(progress, new EspSyncProgress(EspSyncStage.Finalizing, transfer.ReceivedCount, transfer.PersistedCount, transfer.DuplicateCount, transfer.LastPersistedReadingId));

            if (transfer.LastPersistedReadingId > 0)
            {
                var ackPacket = EspControlEncoder.Ack(transfer.BootSessionId, transfer.LastPersistedReadingId);
                await SafeWriteAsync(controlCharacteristic, ackPacket, cancellationToken);
            }

            await SendCancelAsync(controlCharacteristic, cancellationToken);

            return new EspSyncResult(
                transfer.DeviceId,
                transfer.BootSessionId,
                range.OldestReadingId,
                range.LatestReadingId,
                transfer.RequestedAfterId,
                transfer.LastPersistedReadingId,
                transfer.ReceivedCount,
                transfer.PersistedCount,
                transfer.DuplicateCount,
                transfer.HasGap || outcome.HasGap,
                EspSyncStatus.Completed,
                null);
        }
        catch (OperationCanceledException)
        {
            await SendCancelAsync(controlCharacteristic, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                throw new EspSyncException("Synchronization timed out.");
            }

            throw;
        }
        finally
        {
            dataCharacteristic.ValueUpdated -= OnDataUpdated;
            if (subscribed)
            {
                await SendCancelAsync(controlCharacteristic, cancellationToken);
                try
                {
                    await dataCharacteristic.StopUpdatesAsync(cancellationToken);
                }
                catch (Exception)
                {
                }
            }

            _transfer = null;

            if (!retriedSession && transfer.LastPersistedReadingId > 0)
            {
                try
                {
                    await _repository.SaveCursorAsync(new EspSyncCursor
                    {
                        DeviceId = transfer.DeviceId,
                        BootSessionId = transfer.BootSessionId,
                        LastPersistedReadingId = transfer.LastPersistedReadingId,
                        UpdatedAtUtc = DateTimeOffset.UtcNow,
                    });
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private void HandleDataNotification(TransferState transfer, byte[]? data)
    {
        if (!ReferenceEquals(_transfer, transfer) || data is null || data.Length == 0)
        {
            return;
        }

        try
        {
            switch ((EspProtocol.DataPacketType)data[0])
            {
                case EspProtocol.DataPacketType.ReadingMeta:
                    HandleMetaPacket(transfer, data);
                    break;
                case EspProtocol.DataPacketType.ReadingValues:
                    HandleValuesPacket(transfer, data);
                    break;
                case EspProtocol.DataPacketType.TransferEnd:
                    HandleEndPacket(transfer, data);
                    break;
                case EspProtocol.DataPacketType.Error:
                    HandleErrorPacket(transfer, data);
                    break;
            }
        }
        catch (EspProtocolException)
        {
        }
    }

    private void HandleMetaPacket(TransferState transfer, byte[] data)
    {
        var meta = EspPacketParser.ParseMeta(data);
        if (meta.BootSessionId != transfer.BootSessionId || meta.DeviceId != transfer.DeviceId)
        {
            return;
        }

        transfer.PendingMeta[meta.ReadingId] = meta;
    }

    private void HandleValuesPacket(TransferState transfer, byte[] data)
    {
        var values = EspPacketParser.ParseValues(data);
        if (values.BootSessionId != transfer.BootSessionId)
        {
            return;
        }

        if (!transfer.PendingMeta.TryGetValue(values.ReadingId, out var meta))
        {
            return;
        }

        transfer.ReceivedCount++;

        var reading = new EspReading
        {
            DeviceId = transfer.DeviceId,
            BootSessionId = values.BootSessionId,
            ReadingId = values.ReadingId,
            ElapsedSeconds = values.ElapsedSeconds,
            TemperatureCelsius = values.TemperatureCelsius,
            HumidityPercent = values.HumidityPercent,
            Latitude = transfer.PhoneLocation?.Latitude,
            Longitude = transfer.PhoneLocation?.Longitude,
            RecordedAtUtc = EspTimestampCalculator.RecordedAtUtc(
                transfer.PhoneAnchorUtc,
                transfer.EspAnchorUptimeSeconds,
                values.ElapsedSeconds),
            ReceivedAtUtc = DateTimeOffset.UtcNow,
        };

        var inserted = _repository.InsertIdempotentAsync(reading).GetAwaiter().GetResult();
        if (inserted > 0)
        {
            transfer.PersistedCount++;
            if (values.ReadingId > transfer.LastPersistedReadingId)
            {
                transfer.LastPersistedReadingId = values.ReadingId;
            }
        }
        else
        {
            transfer.DuplicateCount++;
        }

        transfer.PendingMeta[values.ReadingId] = meta;
        transfer.Progress?.Report(new EspSyncProgress(
            EspSyncStage.Transferring,
            transfer.ReceivedCount,
            transfer.PersistedCount,
            transfer.DuplicateCount,
            transfer.LastPersistedReadingId));
    }

    private void HandleEndPacket(TransferState transfer, byte[] data)
    {
        var end = EspPacketParser.ParseTransferEnd(data);
        if (end.BootSessionId != transfer.BootSessionId)
        {
            transfer.Complete(new TransferOutcome(TransferOutcomeKind.SessionMismatch));
            return;
        }

        transfer.Complete(new TransferOutcome(TransferOutcomeKind.Ended));
    }

    private void HandleErrorPacket(TransferState transfer, byte[] data)
    {
        var error = EspPacketParser.ParseError(data);
        if (error.BootSessionId != transfer.BootSessionId)
        {
            transfer.Complete(new TransferOutcome(TransferOutcomeKind.SessionMismatch));
            return;
        }

        switch (error.ErrorCode)
        {
            case EspProtocol.EspErrorCode.SessionMismatch:
                transfer.Complete(new TransferOutcome(TransferOutcomeKind.SessionMismatch));
                break;
            case EspProtocol.EspErrorCode.ReadingOverwritten:
                transfer.HasGap = true;
                break;
            case EspProtocol.EspErrorCode.InvalidCommand:
                transfer.Complete(new TransferOutcome(TransferOutcomeKind.InvalidCommand));
                break;
        }
    }

    private async Task<EspDeviceIdentity> ReadIdentityAsync(ICharacteristic characteristic, CancellationToken cancellationToken)
    {
        var (data, resultCode) = await characteristic.ReadAsync(cancellationToken);
        if (resultCode != 0)
        {
            throw new EspSyncException($"Reading Identity failed with BLE result code {resultCode}.");
        }

        return EspPacketParser.ParseIdentity(data);
    }

    private async Task<EspReadingRange> ReadRangeAsync(ICharacteristic characteristic, CancellationToken cancellationToken)
    {
        var (data, resultCode) = await characteristic.ReadAsync(cancellationToken);
        if (resultCode != 0)
        {
            throw new EspSyncException($"Reading Range failed with BLE result code {resultCode}.");
        }

        return EspPacketParser.ParseRange(data);
    }

    private async Task<(uint UptimeSeconds, DateTimeOffset PhoneAnchorUtc)> ReadUptimeAsync(
        ICharacteristic characteristic,
        CancellationToken cancellationToken)
    {
        var phoneAnchorUtc = DateTimeOffset.UtcNow;
        var (data, resultCode) = await characteristic.ReadAsync(cancellationToken);
        if (resultCode != 0)
        {
            throw new EspSyncException($"Reading Uptime failed with BLE result code {resultCode}.");
        }

        return (EspPacketParser.ParseUptime(data), phoneAnchorUtc);
    }

    private async Task<ICharacteristic> GetCharacteristicAsync(
        string deviceIdKey,
        Guid characteristicUuid,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{deviceIdKey}:{characteristicUuid}";
        if (_characteristics.TryGetValue(cacheKey, out var cached) && cached.Service.Device is not null)
        {
            return cached;
        }

        if (_connectedDevice is null)
        {
            throw new EspSyncException("Device is not connected.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CharacteristicReadTimeout);

        var service = await _connectedDevice.GetServiceAsync(EspProtocol.ServiceUuid, cts.Token);
        if (service is null)
        {
            throw new EspSyncException($"Required BLE service {EspProtocol.ServiceUuid} not found on the device.");
        }

        var characteristics = await service.GetCharacteristicsAsync(cts.Token);
        var characteristic = characteristics.FirstOrDefault(c => c.Id == characteristicUuid);
        if (characteristic is null)
        {
            throw new EspSyncException($"Required BLE characteristic {characteristicUuid} not found on the device.");
        }

        _characteristics[cacheKey] = characteristic;
        return characteristic;
    }

    private async Task SendCancelAsync(ICharacteristic controlCharacteristic, CancellationToken cancellationToken)
    {
        await SafeWriteAsync(controlCharacteristic, EspControlEncoder.Cancel(), cancellationToken);
    }

    private async Task SafeWriteAsync(ICharacteristic characteristic, byte[] packet, CancellationToken cancellationToken)
    {
        try
        {
            await characteristic.WriteAsync(packet, cancellationToken);
        }
        catch (Exception)
        {
        }
    }

    private void OnDeviceConnectionLost(object? sender, DeviceErrorEventArgs e)
    {
        var transfer = _transfer;
        if (transfer is not null)
        {
            transfer.Complete(new TransferOutcome(TransferOutcomeKind.Disconnected));
        }

        _connectedDevice = null;
        _connectedDeviceInfo = null;
        _characteristics.Clear();

        if (_connectionLostHandlerAttached)
        {
            ConnectionLost?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool IsEspDevice(IDevice device)
    {
        if (!string.IsNullOrWhiteSpace(device.Name) &&
            device.Name.StartsWith(EspProtocol.DeviceNamePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return device.AdvertisementRecords.Any(record =>
            (record.Type == AdvertisementRecordType.UuidsIncomplete128Bit ||
             record.Type == AdvertisementRecordType.UuidsComplete128Bit) &&
            record.Data.Length == _serviceUuidBytes.Length &&
            record.Data.AsSpan().SequenceEqual(_serviceUuidBytes));
    }

    private EspDeviceInfo CreateDeviceInfo(IDevice device)
    {
        return new EspDeviceInfo(
            device.Id.ToString(),
            device.Name ?? "Unknown ESP",
            device.Id.ToString("N"),
            device.Rssi,
            null,
            false);
    }

    private static byte[] CreateServiceUuidBytes()
    {
        var bytes = new byte[16];
        EspProtocol.ServiceUuid.TryWriteBytes(bytes);
        return bytes;
    }

    private static void Report(IProgress<EspSyncProgress>? progress, EspSyncProgress value)
    {
        progress?.Report(value);
    }

    private enum TransferOutcomeKind
    {
        Ended,
        SessionMismatch,
        InvalidCommand,
        Disconnected,
        Cancelled,
        Timeout,
    }

    private sealed record TransferOutcome(
        TransferOutcomeKind Kind,
        bool HasGap = false);

    private sealed class TransferState
    {
        private readonly TaskCompletionSource<TransferOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completed;

        public TransferState(
            string deviceId,
            uint bootSessionId,
            uint espAnchorUptimeSeconds,
            DateTimeOffset phoneAnchorUtc,
            Location? phoneLocation,
            uint requestedAfterId,
            bool hasGap,
            IProgress<EspSyncProgress>? progress)
        {
            DeviceId = deviceId;
            BootSessionId = bootSessionId;
            EspAnchorUptimeSeconds = espAnchorUptimeSeconds;
            PhoneAnchorUtc = phoneAnchorUtc;
            PhoneLocation = phoneLocation;
            RequestedAfterId = requestedAfterId;
            HasGap = hasGap;
            Progress = progress;
        }

        public string DeviceId { get; }

        public uint BootSessionId { get; }

        public uint EspAnchorUptimeSeconds { get; }

        public DateTimeOffset PhoneAnchorUtc { get; }

        public Location? PhoneLocation { get; }

        public uint RequestedAfterId { get; }

        public bool HasGap { get; set; }

        public Dictionary<uint, EspReadingMeta> PendingMeta { get; } = new();

        public int ReceivedCount { get; set; }

        public int PersistedCount { get; set; }

        public int DuplicateCount { get; set; }

        public uint LastPersistedReadingId { get; set; }

        public Task<TransferOutcome> Completion => _completion.Task;

        public IProgress<EspSyncProgress>? Progress { get; set; }

        public bool Complete(TransferOutcome outcome)
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0)
            {
                return false;
            }

            _completion.TrySetResult(outcome);
            return true;
        }
    }
}
