using System.Collections.ObjectModel;
using BTController.Core.Models;
using BTController.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace BTController.App.ViewModels;

public partial class DiscoveryViewModel : ObservableObject
{
    private readonly IBluetoothService _bluetoothService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    public ObservableCollection<BluetoothDeviceModel> DiscoveredDevices { get; } = new();

    public DiscoveryViewModel()
    {
        _bluetoothService = App.BluetoothService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _bluetoothService.DiscoveredDeviceFound += OnDiscoveredDeviceFound;
        _bluetoothService.DiscoveryStateChanged += (s, state) =>
        {
            _dispatcherQueue.TryEnqueue(() => IsScanning = state);
        };
    }

    [RelayCommand]
    public async Task StartScanAsync()
    {
        DiscoveredDevices.Clear();
        await _bluetoothService.StartDiscoveryAsync();
    }

    [RelayCommand]
    public void StopScan()
    {
        _bluetoothService.StopDiscovery();
    }

    [RelayCommand]
    public async Task PairDeviceAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        device.IsConnecting = true;
        bool success = await _bluetoothService.PairDeviceAsync(device.Id);
        device.IsConnecting = false;
        if (success)
        {
            DiscoveredDevices.Remove(device);
        }
    }

    private void OnDiscoveredDeviceFound(object? sender, BluetoothDeviceModel dev)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!DiscoveredDevices.Any(d => d.Id == dev.Id))
            {
                DiscoveredDevices.Add(dev);
            }
        });
    }
}
