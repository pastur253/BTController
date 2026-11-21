using CommunityToolkit.Mvvm.ComponentModel;

namespace BTController.Core.Models;

public partial class BluetoothDeviceModel : ObservableObject
{
    [ObservableProperty]
    public partial string Id { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsConnected { get; set; }

    [ObservableProperty]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial bool IsDisconnecting { get; set; }

    [ObservableProperty]
    public partial bool IsInWidget { get; set; }

    [ObservableProperty]
    public partial DeviceType DeviceType { get; set; } = DeviceType.Other;

    [ObservableProperty]
    public partial int? BatteryPercent { get; set; }

    [ObservableProperty]
    public partial bool IsPaired { get; set; }

    [ObservableProperty]
    public partial bool CanPair { get; set; }

    public string StatusText => IsConnecting ? "Подключение..."
        : IsDisconnecting ? "Отключение..."
        : IsConnected ? "Подключено"
        : "Отключено";

    public string ActionButtonText => IsConnecting ? "..."
        : IsDisconnecting ? "..."
        : IsConnected ? "Отключить"
        : "Подключить";

    public string StatusGlyph => IsConnected ? "\uE73E" : "\uE711";

    public string TypeGlyph => DeviceType switch
    {
        DeviceType.Headphones => "\uE7F6",
        DeviceType.Speaker => "\uE7F5",
        DeviceType.AudioVideo => "\uE714",
        DeviceType.Phone => "\uE8EA",
        DeviceType.Computer => "\uE7F8",
        DeviceType.Peripheral => "\uE962",
        DeviceType.Wearable => "\uE958",
        _ => "\uE702"
    };

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    partial void OnIsConnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    partial void OnIsDisconnectingChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ActionButtonText));
    }
}
