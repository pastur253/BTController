using System.Collections.ObjectModel;
using BTController.Core.Models;
using BTController.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;

namespace BTController.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IBluetoothService _bluetoothService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public ObservableCollection<BluetoothDeviceModel> WidgetDevices { get; } = new();

    public DashboardViewModel()
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
            var list = await _bluetoothService.GetWidgetDevicesAsync();
            _dispatcherQueue.TryEnqueue(() =>
            {
                WidgetDevices.Clear();
                foreach (var dev in list)
                {
                    WidgetDevices.Add(dev);
                }
            });
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleConnectionAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        await _bluetoothService.ToggleConnectionAsync(device.Id);
    }

    [RelayCommand]
    public async Task RemoveFromWidgetAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        await _bluetoothService.RemoveFromWidgetAsync(device.Id);
        WidgetDevices.Remove(device);
    }

    [RelayCommand]
    public async Task MoveUpAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        int idx = WidgetDevices.IndexOf(device);
        if (idx > 0)
        {
            await _bluetoothService.MoveWidgetDeviceUpAsync(device.Id);
            WidgetDevices.Move(idx, idx - 1);
        }
    }

    [RelayCommand]
    public async Task MoveDownAsync(BluetoothDeviceModel device)
    {
        if (device == null) return;
        int idx = WidgetDevices.IndexOf(device);
        if (idx >= 0 && idx < WidgetDevices.Count - 1)
        {
            await _bluetoothService.MoveWidgetDeviceDownAsync(device.Id);
            WidgetDevices.Move(idx, idx + 1);
        }
    }

    private void OnDeviceUpdated(object? sender, BluetoothDeviceModel updated)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var item = WidgetDevices.FirstOrDefault(d => d.Id == updated.Id);
            if (item != null)
            {
                item.IsConnected = updated.IsConnected;
                item.IsConnecting = updated.IsConnecting;
                item.IsDisconnecting = updated.IsDisconnecting;
            }
        });
    }
}
