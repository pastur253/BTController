using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BTController.Core.Models;

namespace BTController.Widgets;

public static class WidgetPayloadHelper
{
    public static string GeneratePayload(IReadOnlyList<BluetoothDeviceModel>? devices = null)
    {
        return CreateDataPayload(devices);
    }

    public static string CreateDataPayload(IReadOnlyList<BluetoothDeviceModel>? devices)
    {
        var deviceList = devices ?? Array.Empty<BluetoothDeviceModel>();

        var deviceDtoList = deviceList
            .Where(d => d != null)
            .Select(d => new
            {
                id = d.Id ?? string.Empty,
                name = string.IsNullOrEmpty(d.Name) ? "Неизвестное устройство" : d.Name,
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
            deviceCount = deviceList.Count,
            devicesConnectedCount = deviceList.Count(d => d != null && d.IsConnected),
            lastUpdated = DateTime.Now.ToString("HH:mm:ss"),
            devices = deviceDtoList
        };

        return JsonSerializer.Serialize(payload);
    }
}
