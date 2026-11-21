using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using BTController.Core.Models;

namespace BTController.Core.Services;

public class WindowsBluetoothService : IBluetoothService
{
    private readonly WidgetPreferencesService _widgetPrefs;
    private readonly ConcurrentDictionary<string, BluetoothDevice> _activeBtDevices = new();
    private readonly ConcurrentDictionary<string, BluetoothDeviceModel> _cachedModels = new();
    private DeviceWatcher? _pairedWatcher;
    private DeviceWatcher? _discoveryWatcher;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public event EventHandler<BluetoothDeviceModel>? DeviceUpdated;
    public event EventHandler<BluetoothDeviceModel>? DiscoveredDeviceFound;
    public event EventHandler<bool>? DiscoveryStateChanged;
    public event EventHandler? DevicesReloaded;

    public bool IsScanning { get; private set; }
    public bool IsBluetoothEnabled => true; // Determined via Windows.Devices.Radios if needed

    public WindowsBluetoothService(WidgetPreferencesService widgetPrefs)
    {
        _widgetPrefs = widgetPrefs;
        _widgetPrefs.WidgetSettingsChanged += (s, e) => DevicesReloaded?.Invoke(this, EventArgs.Empty);
    }

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await RefreshPairedDevicesInternalAsync();
            StartPairedDevicesWatcher();
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void StartPairedDevicesWatcher()
    {
        string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        _pairedWatcher = DeviceInformation.CreateWatcher(
            selector,
            ["System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.SignalStrength"]);

        _pairedWatcher.Added += async (w, info) =>
        {
            await ProcessOrUpdateDeviceAsync(info.Id);
            DevicesReloaded?.Invoke(this, EventArgs.Empty);
        };

        _pairedWatcher.Removed += (w, info) =>
        {
            if (_cachedModels.TryRemove(info.Id, out _))
            {
                if (_activeBtDevices.TryRemove(info.Id, out var dev))
                {
                    dev.Dispose();
                }
                DevicesReloaded?.Invoke(this, EventArgs.Empty);
            }
        };

        _pairedWatcher.Updated += async (w, update) =>
        {
            await ProcessOrUpdateDeviceAsync(update.Id);
        };

        _pairedWatcher.Start();
    }

    private async Task RefreshPairedDevicesInternalAsync()
    {
        try
        {
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devices = await DeviceInformation.FindAllAsync(selector);
            foreach (var devInfo in devices)
            {
                await ProcessOrUpdateDeviceAsync(devInfo.Id);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BTService] Error refreshing devices: {ex.Message}");
        }
    }

    private async Task<BluetoothDeviceModel?> ProcessOrUpdateDeviceAsync(string deviceId)
    {
        try
        {
            var widgetIds = await _widgetPrefs.GetWidgetDeviceIdsAsync();

            if (!_activeBtDevices.TryGetValue(deviceId, out var btDevice))
            {
                btDevice = await BluetoothDevice.FromIdAsync(deviceId);
                if (btDevice == null) return null;

                _activeBtDevices[deviceId] = btDevice;
                btDevice.ConnectionStatusChanged += OnDeviceConnectionStatusChanged;
            }

            var model = _cachedModels.GetOrAdd(deviceId, id => new BluetoothDeviceModel
            {
                Id = id,
                Address = btDevice.BluetoothAddress.ToString("X12"),
                Name = !string.IsNullOrWhiteSpace(btDevice.Name) ? btDevice.Name : "Bluetooth Device"
            });

            model.Name = !string.IsNullOrWhiteSpace(btDevice.Name) ? btDevice.Name : model.Name;
            model.Address = btDevice.BluetoothAddress.ToString("X12");
            model.IsConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
            model.DeviceType = MapDeviceType(btDevice.ClassOfDevice);
            model.IsPaired = btDevice.DeviceInformation.Pairing.IsPaired;
            model.IsInWidget = widgetIds.Contains(deviceId);

            DeviceUpdated?.Invoke(this, model);
            return model;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BTService] ProcessDevice failed for {deviceId}: {ex.Message}");
            return null;
        }
    }

    private void OnDeviceConnectionStatusChanged(BluetoothDevice sender, object args)
    {
        if (_cachedModels.TryGetValue(sender.DeviceId, out var model))
        {
            model.IsConnected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
            model.IsConnecting = false;
            model.IsDisconnecting = false;
            DeviceUpdated?.Invoke(this, model);
        }
    }

    public async Task<IReadOnlyList<BluetoothDeviceModel>> GetPairedDevicesAsync()
    {
        await InitializeAsync();
        var widgetIds = await _widgetPrefs.GetWidgetDeviceIdsAsync();
        return _cachedModels.Values
            .Where(d => !widgetIds.Contains(d.Id))
            .OrderBy(d => d.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<BluetoothDeviceModel>> GetWidgetDevicesAsync()
    {
        await InitializeAsync();
        var widgetIds = await _widgetPrefs.GetWidgetDeviceIdsAsync();
        var list = new List<BluetoothDeviceModel>();

        foreach (var id in widgetIds)
        {
            if (_cachedModels.TryGetValue(id, out var model))
            {
                model.IsInWidget = true;
                list.Add(model);
            }
            else
            {
                var loaded = await ProcessOrUpdateDeviceAsync(id);
                if (loaded != null)
                {
                    loaded.IsInWidget = true;
                    list.Add(loaded);
                }
            }
        }

        return list;
    }

    public async Task AddToWidgetAsync(string deviceId)
    {
        await _widgetPrefs.AddDeviceAsync(deviceId);
        if (_cachedModels.TryGetValue(deviceId, out var model))
        {
            model.IsInWidget = true;
            DeviceUpdated?.Invoke(this, model);
        }
    }

    public async Task RemoveFromWidgetAsync(string deviceId)
    {
        await _widgetPrefs.RemoveDeviceAsync(deviceId);
        if (_cachedModels.TryGetValue(deviceId, out var model))
        {
            model.IsInWidget = false;
            DeviceUpdated?.Invoke(this, model);
        }
    }

    public async Task MoveWidgetDeviceUpAsync(string deviceId)
    {
        await _widgetPrefs.MoveUpAsync(deviceId);
    }

    public async Task MoveWidgetDeviceDownAsync(string deviceId)
    {
        await _widgetPrefs.MoveDownAsync(deviceId);
    }

    public async Task<bool> ToggleConnectionAsync(string deviceId)
    {
        if (_cachedModels.TryGetValue(deviceId, out var model))
        {
            return model.IsConnected
                ? await DisconnectDeviceAsync(deviceId)
                : await ConnectDeviceAsync(deviceId);
        }

        return await ConnectDeviceAsync(deviceId);
    }

    public async Task<bool> ConnectDeviceAsync(string deviceId)
    {
        if (_cachedModels.TryGetValue(deviceId, out var model))
        {
            model.IsConnecting = true;
            DeviceUpdated?.Invoke(this, model);
        }

        try
        {
            if (!_activeBtDevices.TryGetValue(deviceId, out var btDevice))
            {
                btDevice = await BluetoothDevice.FromIdAsync(deviceId);
                if (btDevice != null) _activeBtDevices[deviceId] = btDevice;
            }

            if (btDevice == null) return false;

            // Trigger baseband ACL / RFCOMM connection in Windows
            var rfcommResult = await btDevice.GetRfcommServicesAsync(BluetoothCacheMode.Uncached);

            try
            {
                // Also attempt BLE connection if supported by the address
                using var leDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(btDevice.BluetoothAddress);
                if (leDevice != null)
                {
                    await leDevice.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                }
            }
            catch
            {
                // Device might not be BLE, ignore
            }

            if (model != null)
            {
                model.IsConnecting = false;
                model.IsConnected = btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
                DeviceUpdated?.Invoke(this, model);
            }

            return btDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BTService] Connect error: {ex.Message}");
            if (model != null)
            {
                model.IsConnecting = false;
                DeviceUpdated?.Invoke(this, model);
            }
            return false;
        }
    }

    public async Task<bool> DisconnectDeviceAsync(string deviceId)
    {
        if (_cachedModels.TryGetValue(deviceId, out var model))
        {
            model.IsDisconnecting = true;
            DeviceUpdated?.Invoke(this, model);
        }

        try
        {
            if (_activeBtDevices.TryRemove(deviceId, out var btDevice))
            {
                btDevice.Dispose();
            }

            // Force garbage collection of COM RCW to release radio ACL handles in Windows
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (model != null)
            {
                model.IsDisconnecting = false;
                model.IsConnected = false;
                DeviceUpdated?.Invoke(this, model);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BTService] Disconnect error: {ex.Message}");
            if (model != null)
            {
                model.IsDisconnecting = false;
                DeviceUpdated?.Invoke(this, model);
            }
            return false;
        }
    }

    public Task StartDiscoveryAsync()
    {
        if (IsScanning) return Task.CompletedTask;

        string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(false);
        _discoveryWatcher = DeviceInformation.CreateWatcher(
            selector,
            ["System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.SignalStrength"]);

        _discoveryWatcher.Added += (w, info) =>
        {
            if (string.IsNullOrWhiteSpace(info.Name)) return;

            var model = new BluetoothDeviceModel
            {
                Id = info.Id,
                Name = info.Name,
                IsPaired = false,
                CanPair = info.Pairing.CanPair,
                DeviceType = DeviceType.Other
            };

            DiscoveredDeviceFound?.Invoke(this, model);
        };

        _discoveryWatcher.Stopped += (w, e) =>
        {
            IsScanning = false;
            DiscoveryStateChanged?.Invoke(this, false);
        };

        _discoveryWatcher.Start();
        IsScanning = true;
        DiscoveryStateChanged?.Invoke(this, true);

        return Task.CompletedTask;
    }

    public void StopDiscovery()
    {
        if (_discoveryWatcher != null && IsScanning)
        {
            _discoveryWatcher.Stop();
            IsScanning = false;
            DiscoveryStateChanged?.Invoke(this, false);
        }
    }

    public async Task<bool> PairDeviceAsync(string deviceId)
    {
        try
        {
            var devInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
            var result = await devInfo.Pairing.PairAsync(DevicePairingProtectionLevel.None);
            return result.Status == DevicePairingResultStatus.Paired ||
                   result.Status == DevicePairingResultStatus.AlreadyPaired;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UnpairDeviceAsync(string deviceId)
    {
        try
        {
            var devInfo = await DeviceInformation.CreateFromIdAsync(deviceId);
            var result = await devInfo.Pairing.UnpairAsync();
            return result.Status == DeviceUnpairingResultStatus.Unpaired ||
                   result.Status == DeviceUnpairingResultStatus.AlreadyUnpaired;
        }
        catch
        {
            return false;
        }
    }

    private static DeviceType MapDeviceType(BluetoothClassOfDevice? cod)
    {
        if (cod == null) return DeviceType.Other;

        return cod.MajorClass switch
        {
            BluetoothMajorClass.AudioVideo => cod.MinorClass switch
            {
                BluetoothMinorClass.AudioVideoHeadphones or
                BluetoothMinorClass.AudioVideoWearableHeadset => DeviceType.Headphones,
                BluetoothMinorClass.AudioVideoLoudspeaker => DeviceType.Speaker,
                _ => DeviceType.AudioVideo
            },
            BluetoothMajorClass.Phone => DeviceType.Phone,
            BluetoothMajorClass.Computer => DeviceType.Computer,
            BluetoothMajorClass.Peripheral => DeviceType.Peripheral,
            BluetoothMajorClass.Wearable => DeviceType.Wearable,
            _ => DeviceType.Other
        };
    }
}
