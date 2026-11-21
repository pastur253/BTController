using System.Collections.ObjectModel;
using BTController.Core.Models;
using BTController.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace BTController.App.ViewModels;

public partial class PairedViewModel : ObservableObject
{
    private readonly IBluetoothService _bluetoothService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public ObservableCollection<BluetoothDeviceModel> PairedDevices { get; } = new();

    public PairedViewModel()
    {
        _bluetoothService = App.BluetoothService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _bluetoothService.DeviceUpdated += OnDeviceUpdated;
        _bluetoothService.DevicesReloaded += async (s, e) => await LoadDevicesAsync();

        _ = LoadDevicesAsync();
    }

    [RelayCommand]
    public async Task LoadDevicesAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _bluetoothService.GetPairedDevicesAsync();
            _dispatcherQueue.TryEnqueue(() =>
            {
                PairedDevices.Clear();
                foreach (var dev in list)
                {
                    PairedDevices.Add(dev);
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddToWidgetAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        await _bluetoothService.AddToWidgetAsync(device.Id);
        PairedDevices.Remove(device);
    }

    [RelayCommand]
    public async Task ToggleConnectionAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        await _bluetoothService.ToggleConnectionAsync(device.Id);
    }

    [RelayCommand]
    public async Task UnpairAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        await _bluetoothService.UnpairDeviceAsync(device.Id);
        PairedDevices.Remove(device);
    }

    private void OnDeviceUpdated(object? sender, BluetoothDeviceModel updated)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var item = PairedDevices.FirstOrDefault(d => d.Id == updated.Id);
            if (item != null)
            {
                item.IsConnected = updated.IsConnected;
                item.IsConnecting = updated.IsConnecting;
                item.IsDisconnecting = updated.IsDisconnecting;
            }
        });
    }
}
