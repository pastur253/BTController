using System.Text.Json;
using BTController.Core.Models;

namespace BTController.Widgets;

public static class WidgetPayloadHelper
{
    public static string CreateDataPayload(IReadOnlyList<BluetoothDeviceModel> devices)
    {
        var deviceDtoList = devices.Select(d => new
        {
            id = d.Id,
            name = d.Name,
            isConnected = d.IsConnected,
            isConnecting = d.IsConnecting,
            statusText = d.IsConnecting ? "Подключение..." : (d.IsConnected ? "Подключено" : "Отключено"),
            statusColor = d.IsConnecting ? "Warning" : (d.IsConnected ? "Good" : "Default"),
            actionTitle = d.IsConnecting ? "..." : (d.IsConnected ? "Откл" : "Вкл"),
            icon = d.DeviceType switch
            {
                DeviceType.Headphones => "🎧",
                DeviceType.Speaker => "🔊",
                DeviceType.Peripheral => "⌨",
                DeviceType.Phone => "📱",
                DeviceType.Computer => "💻",
                DeviceType.Wearable => "⌚",
                _ => "📶"
            },
            battery = d.BatteryPercent
        }).ToList();

        var payload = new
        {
            deviceCount = devices.Count,
            devicesConnectedCount = devices.Count(d => d.IsConnected),
            devices = deviceDtoList
        };

        return JsonSerializer.Serialize(payload);
    }
}
