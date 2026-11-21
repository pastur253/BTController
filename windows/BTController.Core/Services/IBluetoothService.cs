using BTController.Core.Models;

namespace BTController.Core.Services;

public interface IBluetoothService
{
    event EventHandler<BluetoothDeviceModel>? DeviceUpdated;
    event EventHandler<BluetoothDeviceModel>? DiscoveredDeviceFound;
    event EventHandler<bool>? DiscoveryStateChanged;
    event EventHandler? DevicesReloaded;

    bool IsScanning { get; }
    bool IsBluetoothEnabled { get; }

    Task InitializeAsync();
    Task<IReadOnlyList<BluetoothDeviceModel>> GetPairedDevicesAsync();
    Task<IReadOnlyList<BluetoothDeviceModel>> GetWidgetDevicesAsync();
    
    Task AddToWidgetAsync(string deviceId);
    Task RemoveFromWidgetAsync(string deviceId);
    Task MoveWidgetDeviceUpAsync(string deviceId);
    Task MoveWidgetDeviceDownAsync(string deviceId);

    Task<bool> ConnectDeviceAsync(string deviceId);
    Task<bool> DisconnectDeviceAsync(string deviceId);
    Task<bool> ToggleConnectionAsync(string deviceId);

    Task StartDiscoveryAsync();
    void StopDiscovery();

    Task<bool> PairDeviceAsync(string deviceId);
    Task<bool> UnpairDeviceAsync(string deviceId);
}
