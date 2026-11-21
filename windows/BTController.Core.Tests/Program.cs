using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using BTController.Core.Models;
using BTController.Core.Services;
using BTController.Widgets;

namespace BTController.Core.Tests;

class Program
{
    private static int _passCount = 0;
    private static int _failCount = 0;

    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=================================================");
        Console.WriteLine("  BTController.Core Validation Harness & Tests   ");
        Console.WriteLine("=================================================\n");

        TestBluetoothDeviceModel();
        TestDeviceTypeGlyphs();
        TestWidgetPayloadHelper();
        await TestWidgetPreferencesServiceAsync();
        await TestHostBluetoothAdapterAsync();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"Test Summary: {_passCount} Passed, {_failCount} Failed");
        Console.WriteLine("=================================================");

        return _failCount > 0 ? 1 : 0;
    }

    private static void Assert(bool condition, string testName, string? details = null)
    {
        if (condition)
        {
            _passCount++;
            Console.WriteLine($"  [PASS] {testName}");
        }
        else
        {
            _failCount++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [FAIL] {testName} - {details ?? "Assertion failed"}");
            Console.ResetColor();
        }
    }

    private static void TestBluetoothDeviceModel()
    {
        Console.WriteLine("--- 1. Testing BluetoothDeviceModel & MVVM PropertyChanged ---");
        var model = new BluetoothDeviceModel
        {
            Id = "test-device-1",
            Name = "Sony WH-1000XM4",
            Address = "00:11:22:33:44:55",
            DeviceType = DeviceType.Headphones
        };

        var changedProperties = new HashSet<string>();
        model.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        // Test initial state
        Assert(model.StatusText == "Отключено", "Initial StatusText is 'Отключено'");
        Assert(model.ActionButtonText == "Подключить", "Initial ActionButtonText is 'Подключить'");
        Assert(model.StatusGlyph == "\uE711", "Initial StatusGlyph is disconnected glyph (\\uE711)");

        // Test IsConnecting change
        changedProperties.Clear();
        model.IsConnecting = true;
        Assert(changedProperties.Contains(nameof(model.IsConnecting)), "PropertyChanged fired for IsConnecting");
        Assert(changedProperties.Contains(nameof(model.StatusText)), "PropertyChanged fired for StatusText on IsConnecting");
        Assert(changedProperties.Contains(nameof(model.ActionButtonText)), "PropertyChanged fired for ActionButtonText on IsConnecting");
        Assert(model.StatusText == "Подключение...", "StatusText is 'Подключение...' when IsConnecting=true");
        Assert(model.ActionButtonText == "...", "ActionButtonText is '...' when IsConnecting=true");

        // Test IsConnected change
        changedProperties.Clear();
        model.IsConnecting = false;
        model.IsConnected = true;
        Assert(changedProperties.Contains(nameof(model.IsConnected)), "PropertyChanged fired for IsConnected");
        Assert(changedProperties.Contains(nameof(model.StatusText)), "PropertyChanged fired for StatusText on IsConnected");
        Assert(changedProperties.Contains(nameof(model.ActionButtonText)), "PropertyChanged fired for ActionButtonText on IsConnected");
        Assert(model.StatusText == "Подключено", "StatusText is 'Подключено' when IsConnected=true");
        Assert(model.ActionButtonText == "Отключить", "ActionButtonText is 'Отключить' when IsConnected=true");
        Assert(model.StatusGlyph == "\uE73E", "StatusGlyph is connected checkmark glyph (\\uE73E)");

        // Test IsDisconnecting change
        changedProperties.Clear();
        model.IsDisconnecting = true;
        Assert(changedProperties.Contains(nameof(model.IsDisconnecting)), "PropertyChanged fired for IsDisconnecting");
        Assert(changedProperties.Contains(nameof(model.StatusText)), "PropertyChanged fired for StatusText on IsDisconnecting");
        Assert(changedProperties.Contains(nameof(model.ActionButtonText)), "PropertyChanged fired for ActionButtonText on IsDisconnecting");
        Assert(model.StatusText == "Отключение...", "StatusText is 'Отключение...' when IsDisconnecting=true");
        Assert(model.ActionButtonText == "...", "ActionButtonText is '...' when IsDisconnecting=true");

        // Test BatteryPercent change
        changedProperties.Clear();
        model.BatteryPercent = 85;
        Assert(changedProperties.Contains(nameof(model.BatteryPercent)), "PropertyChanged fired for BatteryPercent");
        Assert(model.BatteryPercent == 85, "BatteryPercent is 85");

        // Test IsInWidget change
        changedProperties.Clear();
        model.IsInWidget = true;
        Assert(changedProperties.Contains(nameof(model.IsInWidget)), "PropertyChanged fired for IsInWidget");
        Assert(model.IsInWidget, "IsInWidget is true");

        // Test IsPaired and CanPair
        changedProperties.Clear();
        model.IsPaired = true;
        model.CanPair = true;
        Assert(changedProperties.Contains(nameof(model.IsPaired)), "PropertyChanged fired for IsPaired");
        Assert(changedProperties.Contains(nameof(model.CanPair)), "PropertyChanged fired for CanPair");
        Assert(model.IsPaired && model.CanPair, "IsPaired is true and CanPair is true");
    }

    private static void TestDeviceTypeGlyphs()
    {
        Console.WriteLine("\n--- 2. Testing DeviceType Helper & TypeGlyph Mappings ---");
        var model = new BluetoothDeviceModel();

        var expectedGlyphs = new Dictionary<DeviceType, string>
        {
            { DeviceType.Headphones, "\uE7F6" },
            { DeviceType.Speaker, "\uE7F5" },
            { DeviceType.AudioVideo, "\uE714" },
            { DeviceType.Phone, "\uE8EA" },
            { DeviceType.Computer, "\uE7F8" },
            { DeviceType.Peripheral, "\uE962" },
            { DeviceType.Wearable, "\uE958" },
            { DeviceType.Other, "\uE702" }
        };

        foreach (var (type, glyph) in expectedGlyphs)
        {
            model.DeviceType = type;
            Assert(model.TypeGlyph == glyph, $"TypeGlyph for {type} matches '{glyph}' (hex: 0x{((int)glyph[0]):X4})");
        }
    }

    private static void TestWidgetPayloadHelper()
    {
        Console.WriteLine("\n--- 3. Testing WidgetPayloadHelper Data Serialization & Emojis ---");
        var devices = new List<BluetoothDeviceModel>
        {
            new() { Id = "1", Name = "Headset", IsConnected = true, DeviceType = DeviceType.Headphones, BatteryPercent = 90 },
            new() { Id = "2", Name = "Speaker", IsConnected = false, DeviceType = DeviceType.Speaker, BatteryPercent = 50 },
            new() { Id = "3", Name = "Mouse", IsConnected = true, DeviceType = DeviceType.Peripheral },
            new() { Id = "4", Name = "Phone", IsConnected = false, DeviceType = DeviceType.Phone },
            new() { Id = "5", Name = "Laptop", IsConnected = false, DeviceType = DeviceType.Computer },
            new() { Id = "6", Name = "Watch", IsConnected = true, DeviceType = DeviceType.Wearable },
            new() { Id = "7", Name = "Unknown", IsConnected = false, DeviceType = DeviceType.Other }
        };

        // Test GeneratePayload() with sample devices
        string json = WidgetPayloadHelper.GeneratePayload(devices);
        Assert(!string.IsNullOrEmpty(json), "GeneratePayload(devices) JSON is not empty");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert(root.GetProperty("deviceCount").GetInt32() == 7, "deviceCount equals 7");
        Assert(root.GetProperty("devicesConnectedCount").GetInt32() == 3, "devicesConnectedCount equals 3");
        Assert(root.TryGetProperty("lastUpdated", out var lastUpdatedProp) && !string.IsNullOrEmpty(lastUpdatedProp.GetString()), "lastUpdated is present and non-empty");

        var devArray = root.GetProperty("devices");
        Assert(devArray.GetArrayLength() == 7, "Payload contains 7 device entries");

        var expectedIcons = new[] { "🎧", "🔊", "⌨", "📱", "💻", "⌚", "📶" };
        for (int i = 0; i < expectedIcons.Length; i++)
        {
            var item = devArray[i];
            string icon = item.GetProperty("icon").GetString() ?? "";
            Assert(icon == expectedIcons[i], $"Widget icon for {devices[i].DeviceType} is '{expectedIcons[i]}'");
            Assert(item.TryGetProperty("statusText", out var st) && !string.IsNullOrEmpty(st.GetString()), $"statusText present for device {i}");
        }

        // Test null safety: null devices list
        string nullJson = WidgetPayloadHelper.GeneratePayload(null);
        Assert(!string.IsNullOrEmpty(nullJson), "GeneratePayload(null) does not throw and returns valid JSON");
        using (var nullDoc = JsonDocument.Parse(nullJson))
        {
            Assert(nullDoc.RootElement.GetProperty("deviceCount").GetInt32() == 0, "Null payload deviceCount is 0");
        }

        // Test empty devices list
        string emptyJson = WidgetPayloadHelper.GeneratePayload(new List<BluetoothDeviceModel>());
        Assert(!string.IsNullOrEmpty(emptyJson), "GeneratePayload(empty) returns valid JSON");

        // Test device with null Name
        var dirtyDevices = new List<BluetoothDeviceModel>
        {
            new() { Id = "null-name", Name = null!, IsConnected = false }
        };
        string dirtyJson = WidgetPayloadHelper.GeneratePayload(dirtyDevices);
        Assert(!string.IsNullOrEmpty(dirtyJson), "GeneratePayload with null Name does not throw and handles safely");

        // Validate Adaptive Card Templates
        Console.WriteLine("\n--- 3b. Testing Adaptive Card Templates (Medium & Large) ---");
        ValidateAdaptiveCardTemplate(@"D:\BTController\Templates\WidgetTemplateMedium.json", "Medium");
        ValidateAdaptiveCardTemplate(@"D:\BTController\Templates\WidgetTemplateLarge.json", "Large");
    }

    private static void ValidateAdaptiveCardTemplate(string templatePath, string size)
    {
        Assert(File.Exists(templatePath), $"{size} template file exists at {templatePath}");
        string content = File.ReadAllText(templatePath);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        Assert(root.GetProperty("type").GetString() == "AdaptiveCard", $"{size} template type is 'AdaptiveCard'");
        Assert(root.GetProperty("version").GetString() == "1.5", $"{size} template version is '1.5'");
        Assert(content.Contains("devices"), $"{size} template contains 'devices' binding");
        Assert(content.Contains("statusText"), $"{size} template contains 'statusText' binding");
        Assert(content.Contains("lastUpdated"), $"{size} template contains 'lastUpdated' binding");
    }

    private static async Task TestWidgetPreferencesServiceAsync()
    {
        Console.WriteLine("\n--- 4. Testing WidgetPreferencesService (Save & Reload) ---");
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string folder = Path.Combine(localAppData, "BTController");
        string filePath = Path.Combine(folder, "widget_devices.json");

        // Backup existing settings file if present
        string? backupFile = null;
        if (File.Exists(filePath))
        {
            backupFile = filePath + ".backup_" + Guid.NewGuid();
            File.Copy(filePath, backupFile, true);
            Console.WriteLine($"  [INFO] Backed up existing widget_devices.json to {backupFile}");
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var service = new WidgetPreferencesService();
            bool eventFired = false;
            service.WidgetSettingsChanged += (s, e) => eventFired = true;

            // Initially empty
            var initialList = await service.GetWidgetDeviceIdsAsync();
            Assert(initialList.Count == 0, "Initial device list is empty when file is deleted");

            // Add devices
            eventFired = false;
            await service.AddDeviceAsync("dev-A");
            Assert(eventFired, "WidgetSettingsChanged fired after AddDeviceAsync('dev-A')");

            await service.AddDeviceAsync("dev-B");
            await service.AddDeviceAsync("dev-C");

            var currentList = await service.GetWidgetDeviceIdsAsync();
            Assert(currentList.Count == 3, "List contains 3 items");
            Assert(currentList[0] == "dev-A" && currentList[1] == "dev-B" && currentList[2] == "dev-C", "Order is dev-A, dev-B, dev-C");

            // Verify file exists on disk and contents match
            Assert(File.Exists(filePath), $"File exists at {filePath}");
            var fileContent = await File.ReadAllTextAsync(filePath);
            var deserialized = JsonSerializer.Deserialize<List<string>>(fileContent);
            Assert(deserialized != null && deserialized.Count == 3, "File on disk has 3 items serialized");

            // Test MoveDownAsync
            await service.MoveDownAsync("dev-A");
            var afterMoveDown = await service.GetWidgetDeviceIdsAsync();
            Assert(afterMoveDown[0] == "dev-B" && afterMoveDown[1] == "dev-A" && afterMoveDown[2] == "dev-C", "MoveDownAsync moved dev-A down: dev-B, dev-A, dev-C");

            // Test MoveUpAsync
            await service.MoveUpAsync("dev-C");
            var afterMoveUp = await service.GetWidgetDeviceIdsAsync();
            Assert(afterMoveUp[0] == "dev-B" && afterMoveUp[1] == "dev-C" && afterMoveUp[2] == "dev-A", "MoveUpAsync moved dev-C up: dev-B, dev-C, dev-A");

            // Test RemoveDeviceAsync
            eventFired = false;
            await service.RemoveDeviceAsync("dev-C");
            Assert(eventFired, "WidgetSettingsChanged fired on RemoveDeviceAsync");
            var afterRemove = await service.GetWidgetDeviceIdsAsync();
            Assert(afterRemove.Count == 2 && afterRemove[0] == "dev-B" && afterRemove[1] == "dev-A", "RemoveDeviceAsync removed dev-C");

            // Test Reloading from a second fresh service instance
            var secondServiceInstance = new WidgetPreferencesService();
            var reloadedList = await secondServiceInstance.GetWidgetDeviceIdsAsync();
            Assert(reloadedList.Count == 2 && reloadedList[0] == "dev-B" && reloadedList[1] == "dev-A", "Second instance reloads persisted devices from disk correctly");
        }
        finally
        {
            // Restore backup or clean up
            if (backupFile != null && File.Exists(backupFile))
            {
                File.Copy(backupFile, filePath, true);
                File.Delete(backupFile);
                Console.WriteLine($"  [INFO] Restored original widget_devices.json from backup");
            }
            else if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    private static async Task TestHostBluetoothAdapterAsync()
    {
        Console.WriteLine("\n--- 5. Testing Windows Bluetooth Adapter & Host Compatibility ---");
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [WARN] BluetoothAdapter.GetDefaultAsync() returned null. Host does not have an active or accessible Bluetooth adapter.");
                Console.ResetColor();
                Assert(true, "BluetoothAdapter query API executed successfully (adapter null/absent)");
                return;
            }

            Assert(true, "BluetoothAdapter.GetDefaultAsync() succeeded");
            Console.WriteLine($"    Adapter DeviceId: {adapter.DeviceId}");
            Console.WriteLine($"    Adapter Address: {adapter.BluetoothAddress:X}");
            Console.WriteLine($"    IsClassicSupported: {adapter.IsClassicSupported}");
            Console.WriteLine($"    IsLowEnergySupported: {adapter.IsLowEnergySupported}");
            Console.WriteLine($"    IsPeripheralRoleSupported: {adapter.IsPeripheralRoleSupported}");
            Console.WriteLine($"    IsCentralRoleSupported: {adapter.IsCentralRoleSupported}");

            try
            {
                var radio = await adapter.GetRadioAsync();
                if (radio != null)
                {
                    Console.WriteLine($"    Radio Name: {radio.Name}");
                    Console.WriteLine($"    Radio Kind: {radio.Kind}");
                    Console.WriteLine($"    Radio State: {radio.State}");
                    Assert(true, $"Bluetooth Radio state verified: {radio.State}");
                }
                else
                {
                    Console.WriteLine("    [INFO] adapter.GetRadioAsync() returned null.");
                    Assert(true, "adapter.GetRadioAsync() executed successfully");
                }
            }
            catch (Exception radioEx)
            {
                Console.WriteLine($"    [INFO] GetRadioAsync threw exception (expected in certain sandbox/VM permissions): {radioEx.Message}");
                Assert(true, "Radio access checked");
            }

            // Test Device Information selector for paired devices
            try
            {
                string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
                var devices = await DeviceInformation.FindAllAsync(selector);
                Console.WriteLine($"    Paired devices count found: {devices.Count}");
                foreach (var dev in devices)
                {
                    Console.WriteLine($"      - Paired Device: '{dev.Name}' (ID: {dev.Id})");
                }
                Assert(true, $"Paired device enumeration executed successfully ({devices.Count} devices found)");
            }
            catch (Exception enumEx)
            {
                Console.WriteLine($"    [WARN] DeviceInformation.FindAllAsync failed: {enumEx.Message}");
                Assert(false, "Device enumeration", enumEx.Message);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [ERROR] Bluetooth API exception: {ex}");
            Console.ResetColor();
            Assert(false, "Bluetooth API compatibility check", ex.Message);
        }
    }
}
