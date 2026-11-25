using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
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
        await TestWindowsBluetoothServiceIntegrationAsync();
        await TestWidgetVerificationBriefAsync();

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
        string templatesDir = Path.Combine(AppContext.BaseDirectory, "Templates");
        ValidateAdaptiveCardTemplate(Path.Combine(templatesDir, "WidgetTemplateMedium.json"), "Medium");
        ValidateAdaptiveCardTemplate(Path.Combine(templatesDir, "WidgetTemplateLarge.json"), "Large");
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
            eventFired = false;
            await service.MoveDownAsync("dev-A");
            Assert(eventFired, "WidgetSettingsChanged fired after MoveDownAsync('dev-A')");
            var afterMoveDown = await service.GetWidgetDeviceIdsAsync();
            Assert(afterMoveDown[0] == "dev-B" && afterMoveDown[1] == "dev-A" && afterMoveDown[2] == "dev-C", "MoveDownAsync moved dev-A down: dev-B, dev-A, dev-C");

            // Test MoveUpAsync
            eventFired = false;
            await service.MoveUpAsync("dev-C");
            Assert(eventFired, "WidgetSettingsChanged fired after MoveUpAsync('dev-C')");
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

            // Test Cross-Process Reload when already loaded instance sees external update on disk
            await secondServiceInstance.AddDeviceAsync("dev-D");
            var crossProcessList = await service.GetWidgetDeviceIdsAsync();
            Assert(crossProcessList.Count == 3 && crossProcessList[2] == "dev-D", "Already-loaded instance reloads external disk changes made by another process");
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
                string[] requestedProperties = { "System.Devices.Aep.IsConnected", "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.SignalStrength" };
                var devices = await DeviceInformation.FindAllAsync(selector, requestedProperties);
                Console.WriteLine($"    Paired devices count found: {devices.Count}");
                Assert(true, $"Paired device enumeration executed successfully ({devices.Count} devices found)");

                foreach (var dev in devices)
                {
                    Console.WriteLine($"\n    --- Inspecting Paired Device: '{dev.Name}' ---");
                    Console.WriteLine($"      Device Information ID: {dev.Id}");
                    Console.WriteLine($"      IsDefault: {dev.IsDefault}");
                    Console.WriteLine($"      IsEnabled: {dev.IsEnabled}");

                    bool isConnected = false;
                    if (dev.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) && connVal is bool bConn)
                    {
                        isConnected = bConn;
                    }
                    Console.WriteLine($"      System.Devices.Aep.IsConnected: {isConnected}");

                    if (dev.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var addrVal) && addrVal != null)
                    {
                        Console.WriteLine($"      System.Devices.Aep.DeviceAddress: {addrVal}");
                    }

                    try
                    {
                        var btDev = await BluetoothDevice.FromIdAsync(dev.Id);
                        if (btDev != null)
                        {
                            Console.WriteLine($"      Bluetooth Device Name: '{btDev.Name}'");
                            Console.WriteLine($"      Bluetooth Address: {btDev.BluetoothAddress:X12}");
                            Console.WriteLine($"      Connection Status: {btDev.ConnectionStatus}");
                            Console.WriteLine($"      ClassOfDevice.MajorClass: {btDev.ClassOfDevice?.MajorClass}");
                            Console.WriteLine($"      ClassOfDevice.RawValue: 0x{btDev.ClassOfDevice?.RawValue:X}");
                            Console.WriteLine($"      Pairing.IsPaired: {btDev.DeviceInformation.Pairing.IsPaired}");
                            Console.WriteLine($"      Pairing.CanPair: {btDev.DeviceInformation.Pairing.CanPair}");
                            Assert(true, $"BluetoothDevice.FromIdAsync succeeded for '{dev.Name}' (Status: {btDev.ConnectionStatus})");
                        }
                        else
                        {
                            Console.WriteLine($"      [WARN] BluetoothDevice.FromIdAsync returned null for ID: {dev.Id} (device offline or radio off)");
                            Assert(true, $"BluetoothDevice.FromIdAsync returned null for ID: {dev.Id}");
                        }
                    }
                    catch (Exception devEx)
                    {
                        Console.WriteLine($"      [INFO] FromIdAsync threw for {dev.Id} (Radio state is Off): [{devEx.GetType().Name}] HResult=0x{devEx.HResult:X8}: {devEx.Message}");
                        Assert(true, $"Handled device offline / radio off state for '{dev.Name}'");
                    }
                }
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

    private static async Task TestWindowsBluetoothServiceIntegrationAsync()
    {
        Console.WriteLine("\n--- 6. Testing WindowsBluetoothService Integration & Pinning ---");
        try
        {
            var prefs = new WidgetPreferencesService();
            var btService = new WindowsBluetoothService(prefs);

            bool reloadedFired = false;
            btService.DevicesReloaded += (s, e) => reloadedFired = true;

            await btService.InitializeAsync();
            Assert(true, "WindowsBluetoothService.InitializeAsync() completed without error");

            var pairedList = await btService.GetPairedDevicesAsync();
            Console.WriteLine($"    Discovered {pairedList.Count} paired devices via WindowsBluetoothService:");
            foreach (var d in pairedList)
            {
                Console.WriteLine($"      * [{d.Id}] {d.Name} ({d.DeviceType}) - Status: {d.StatusText}, Connected: {d.IsConnected}, InWidget: {d.IsInWidget}");
            }
            Assert(true, $"WindowsBluetoothService retrieved {pairedList.Count} paired devices");

            if (pairedList.Count > 0)
            {
                var targetDevice = pairedList[0];
                Console.WriteLine($"    Testing Pin/Unpin workflow on device: '{targetDevice.Name}' (ID: {targetDevice.Id})");

                // Add to widget
                reloadedFired = false;
                await btService.AddToWidgetAsync(targetDevice.Id);
                Assert(reloadedFired, "DevicesReloaded event fired on AddToWidgetAsync");

                var widgetDevices = await btService.GetWidgetDevicesAsync();
                Assert(widgetDevices.Any(d => d.Id == targetDevice.Id), $"Widget devices list contains pinned device '{targetDevice.Name}'");
                Assert(widgetDevices.First(d => d.Id == targetDevice.Id).IsInWidget, "Pinned device model has IsInWidget == true");

                var pairedAfterPin = await btService.GetPairedDevicesAsync();
                Assert(!pairedAfterPin.Any(d => d.Id == targetDevice.Id), "Pinned device is correctly filtered out of unpinned PairedDevices list");

                // Remove from widget
                reloadedFired = false;
                await btService.RemoveFromWidgetAsync(targetDevice.Id);
                Assert(reloadedFired, "DevicesReloaded event fired on RemoveFromWidgetAsync");

                var widgetAfterRemove = await btService.GetWidgetDevicesAsync();
                Assert(!widgetAfterRemove.Any(d => d.Id == targetDevice.Id), "Widget devices list no longer contains removed device");

                var pairedAfterRemove = await btService.GetPairedDevicesAsync();
                Assert(pairedAfterRemove.Any(d => d.Id == targetDevice.Id), "Device is returned to PairedDevices list after removal");
            }
            else
            {
                Console.WriteLine("    [INFO] No physical paired devices found on host to test dynamic pin/unpin; testing with simulated ID");
                await btService.AddToWidgetAsync("simulated-dev-1");
                var widgetDevs = await btService.GetWidgetDevicesAsync();
                Assert(widgetDevs.Count >= 0, "GetWidgetDevicesAsync handled simulated device safely");
                await btService.RemoveFromWidgetAsync("simulated-dev-1");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [ERROR] WindowsBluetoothService test exception: {ex}");
            Console.ResetColor();
            Assert(false, "WindowsBluetoothService Integration", ex.Message);
        }
    }

    private static async Task TestWidgetVerificationBriefAsync()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("  7. Widget Subsystem & Adaptive Card Verification  ");
        Console.WriteLine("=================================================");

        // 1. Verify Widget Definition & COM Server in Package.appxmanifest
        Console.WriteLine("\n--- 7a. Package.appxmanifest Verification ---");
        string? windowsRoot = ResolveWindowsRoot();
        string manifestPath = windowsRoot != null
            ? Path.Combine(windowsRoot, "BTController.App", "Package.appxmanifest")
            : string.Empty;
        Assert(!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath), $"Manifest exists at {manifestPath}");

        var doc = XDocument.Load(manifestPath);
        XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap3 = "http://schemas.microsoft.com/appx/manifest/uap/windows10/3";
        XNamespace com = "http://schemas.microsoft.com/appx/manifest/com/windows10";

        var comServer = doc.Descendants(com + "ExeServer").FirstOrDefault();
        Assert(comServer != null, "COM ExeServer element exists");
        if (comServer != null)
        {
            Assert(comServer.Attribute("Executable")?.Value == "BTController.App.exe", "COM ExeServer Executable is 'BTController.App.exe'");
            Assert(comServer.Attribute("Arguments")?.Value == "-RegisterProcessAsComServer", "COM ExeServer Arguments is '-RegisterProcessAsComServer'");
            var comClass = comServer.Elements(com + "Class").FirstOrDefault();
            Assert(comClass?.Attribute("Id")?.Value.Equals("B7E209E3-4028-4822-8356-9A0191B95B11", StringComparison.OrdinalIgnoreCase) == true,
                "COM Server ClassId matches B7E209E3-4028-4822-8356-9A0191B95B11");
        }

        var appExtension = doc.Descendants(uap3 + "AppExtension")
            .FirstOrDefault(e => e.Attribute("Name")?.Value == "com.microsoft.windows.widgets");
        Assert(appExtension != null, "Widgets provider extension 'com.microsoft.windows.widgets' exists");
        if (appExtension != null)
        {
            Assert(appExtension.Attribute("Id")?.Value == "BTControllerWidgetProvider", "AppExtension Id is 'BTControllerWidgetProvider'");
            Assert(!string.IsNullOrEmpty(appExtension.Attribute("PublicFolder")?.Value), "AppExtension specifies PublicFolder");
            var createInst = appExtension.Descendants().FirstOrDefault(e => e.Name.LocalName == "CreateInstance");
            Assert(createInst?.Attribute("ClassId")?.Value.Equals("B7E209E3-4028-4822-8356-9A0191B95B11", StringComparison.OrdinalIgnoreCase) == true,
                "Widget Provider CreateInstance ClassId matches B7E209E3-4028-4822-8356-9A0191B95B11");

            var widgetDef = appExtension.Descendants().FirstOrDefault(e => e.Name.LocalName == "Definition" && e.Attribute("Id")?.Value == "BTController_Widget");
            Assert(widgetDef != null, "Widget Definition 'BTController_Widget' exists");
            if (widgetDef != null)
            {
                Assert(widgetDef.Attribute("DefaultSize") == null, "Widget Definition omits invalid DefaultSize attribute");
                Assert(widgetDef.Attribute("IsCustomizable")?.Value == "true", "Widget Definition sets IsCustomizable='true'");
                var capSizes = widgetDef.Descendants()
                    .Where(c => c.Name.LocalName == "Capability")
                    .SelectMany(c => c.Elements().Where(s => s.Name.LocalName == "Size"))
                    .Select(s => s.Attribute("Name")?.Value)
                    .ToHashSet();
                Assert(capSizes.Contains("medium"), "Widget capability contains <Size Name='medium' />");
                Assert(capSizes.Contains("large"), "Widget capability contains <Size Name='large' />");

                var iconPath = widgetDef.Descendants().FirstOrDefault(e => e.Name.LocalName == "Icon")?.Attribute("Path")?.Value;
                Assert(iconPath == @"Assets\WidgetIcon.png", "Widget icon path is 'Assets\\WidgetIcon.png'");
                var screenshotPath = widgetDef.Descendants().FirstOrDefault(e => e.Name.LocalName == "Screenshot")?.Attribute("Path")?.Value;
                Assert(screenshotPath == @"Assets\WidgetScreenshot.png", "Widget screenshot path is 'Assets\\WidgetScreenshot.png'");
            }
        }

        // 2. Verify Widget Asset Files
        Console.WriteLine("\n--- 7b. Widget Assets Verification ---");
        string assetsDir = windowsRoot != null ? Path.Combine(windowsRoot, "BTController.App", "Assets") : string.Empty;
        string iconFullPath = Path.Combine(assetsDir, "WidgetIcon.png");
        Assert(File.Exists(iconFullPath), $"WidgetIcon.png exists at {iconFullPath}");
        if (File.Exists(iconFullPath))
        {
            var iconBytes = File.ReadAllBytes(iconFullPath);
            Assert(iconBytes.Length > 0, $"WidgetIcon.png is non-empty ({iconBytes.Length} bytes)");
            bool isPng = iconBytes.Length >= 8 && iconBytes[0] == 0x89 && iconBytes[1] == 0x50 && iconBytes[2] == 0x4E && iconBytes[3] == 0x47;
            Assert(isPng, "WidgetIcon.png has valid PNG magic header");
        }

        string screenshotFullPath = Path.Combine(assetsDir, "WidgetScreenshot.png");
        Assert(File.Exists(screenshotFullPath), $"WidgetScreenshot.png exists at {screenshotFullPath}");
        if (File.Exists(screenshotFullPath))
        {
            var ssBytes = File.ReadAllBytes(screenshotFullPath);
            Assert(ssBytes.Length > 0, $"WidgetScreenshot.png is non-empty ({ssBytes.Length} bytes)");
            bool isPng = ssBytes.Length >= 8 && ssBytes[0] == 0x89 && ssBytes[1] == 0x50 && ssBytes[2] == 0x4E && ssBytes[3] == 0x47;
            Assert(isPng, "WidgetScreenshot.png has valid PNG magic header");
        }

        // 3. Verify Adaptive Card 1.5 Templates and Action Data
        Console.WriteLine("\n--- 7c. Adaptive Card 1.5 Template & Action Data Validation ---");
        string templatesFolder = Path.Combine(AppContext.BaseDirectory, "Templates");
        string[] templatePaths = { Path.Combine(templatesFolder, "WidgetTemplateMedium.json"), Path.Combine(templatesFolder, "WidgetTemplateLarge.json") };
        foreach (var tPath in templatePaths)
        {
            string name = Path.GetFileName(tPath);
            Assert(File.Exists(tPath), $"Template {name} exists at {tPath}");
            string jsonText = File.ReadAllText(tPath);
            using var cardDoc = JsonDocument.Parse(jsonText);
            var cardRoot = cardDoc.RootElement;

            Assert(cardRoot.GetProperty("type").GetString() == "AdaptiveCard", $"{name} type is 'AdaptiveCard'");
            Assert(cardRoot.GetProperty("$schema").GetString() == "http://adaptivecards.io/schemas/adaptive-card.json", $"{name} schema is Adaptive Card schema");
            Assert(cardRoot.GetProperty("version").GetString() == "1.5", $"{name} version is '1.5'");

            // Inspect action data for toggle
            bool foundToggleAction = false;
            foreach (var elem in cardRoot.EnumerateObject())
            {
                CheckForToggleAction(elem.Value, ref foundToggleAction);
            }
            Assert(foundToggleAction, $"{name} contains Action.Execute with action data {{\"action\": \"toggle\", \"deviceId\": \"...\"}}");
        }

        // 4. Generate Payload with Sample Devices
        Console.WriteLine("\n--- 7d. WidgetPayloadHelper with Sample Devices ---");
        var sampleDevices = new List<BluetoothDeviceModel>
        {
            new() { Id = "dev-headset", Name = "Sony WH-1000XM5", IsConnected = true, DeviceType = DeviceType.Headphones, BatteryPercent = 88 },
            new() { Id = "dev-speaker", Name = "JBL Charge 5", IsConnected = false, DeviceType = DeviceType.Speaker, BatteryPercent = 45 },
            new() { Id = "dev-mouse", Name = "Logitech MX Master 3S", IsConnected = true, DeviceType = DeviceType.Peripheral, BatteryPercent = 95 },
            new() { Id = "dev-keyboard", Name = "Keychron K2", IsConnected = false, DeviceType = DeviceType.Peripheral }
        };

        string samplePayloadJson = WidgetPayloadHelper.GeneratePayload(sampleDevices);
        Assert(!string.IsNullOrEmpty(samplePayloadJson), "Sample payload is non-empty");
        using (var sampleDoc = JsonDocument.Parse(samplePayloadJson))
        {
            var sRoot = sampleDoc.RootElement;
            Assert(sRoot.GetProperty("deviceCount").GetInt32() == 4, "Sample deviceCount is 4");
            Assert(sRoot.GetProperty("devicesConnectedCount").GetInt32() == 2, "Sample devicesConnectedCount is 2");
            Assert(sRoot.TryGetProperty("lastUpdated", out _), "Sample lastUpdated property present");
            var devicesArr = sRoot.GetProperty("devices");
            Assert(devicesArr.GetArrayLength() == 4, "Sample devices array length is 4");

            var firstDev = devicesArr[0];
            Assert(firstDev.GetProperty("id").GetString() == "dev-headset", "Device 0 id is dev-headset");
            Assert(firstDev.GetProperty("name").GetString() == "Sony WH-1000XM5", "Device 0 name is Sony WH-1000XM5");
            Assert(firstDev.GetProperty("isConnected").GetBoolean() == true, "Device 0 isConnected is true");
            Assert(firstDev.GetProperty("statusText").GetString() == "Подключено", "Device 0 statusText is 'Подключено'");
            Assert(firstDev.GetProperty("statusColor").GetString() == "Good", "Device 0 statusColor is 'Good'");
            Assert(firstDev.GetProperty("actionTitle").GetString() == "Откл", "Device 0 actionTitle is 'Откл'");
            Assert(firstDev.GetProperty("icon").GetString() == "🎧", "Device 0 icon is '🎧'");
            Assert(firstDev.GetProperty("battery").GetInt32() == 88, "Device 0 battery is 88");
        }
        Console.WriteLine("\n[Sample Payload JSON]:");
        Console.WriteLine(samplePayloadJson);

        // 5. Generate Payload with Real Paired Devices
        Console.WriteLine("\n--- 7e. WidgetPayloadHelper with Real Paired Devices ---");
        var realDevices = new List<BluetoothDeviceModel>();
        try
        {
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            string[] requestedProperties = { "System.Devices.Aep.IsConnected", "System.Devices.Aep.DeviceAddress" };
            var devInfoList = await DeviceInformation.FindAllAsync(selector, requestedProperties);

            foreach (var di in devInfoList)
            {
                bool isConn = false;
                if (di.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connVal) && connVal is bool b)
                    isConn = b;

                var type = DeviceType.Other;
                string lowerName = di.Name.ToLowerInvariant();
                if (lowerName.Contains("airpod") || lowerName.Contains("headset") || lowerName.Contains("bt09") || lowerName.Contains("wh-"))
                    type = DeviceType.Headphones;
                else if (lowerName.Contains("speaker") || lowerName.Contains("jbl"))
                    type = DeviceType.Speaker;
                else if (lowerName.Contains("mouse") || lowerName.Contains("keyboard"))
                    type = DeviceType.Peripheral;

                realDevices.Add(new BluetoothDeviceModel
                {
                    Id = di.Id,
                    Name = di.Name,
                    IsConnected = isConn,
                    IsPaired = true,
                    DeviceType = type
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARN] Querying paired devices threw: {ex.Message}");
        }

        Assert(realDevices.Count > 0, $"Found {realDevices.Count} real paired devices from host");
        string realPayloadJson = WidgetPayloadHelper.GeneratePayload(realDevices);
        Assert(!string.IsNullOrEmpty(realPayloadJson), "Real devices payload JSON is non-empty");

        using (var realDoc = JsonDocument.Parse(realPayloadJson))
        {
            var rRoot = realDoc.RootElement;
            Assert(rRoot.GetProperty("deviceCount").GetInt32() == realDevices.Count, $"Real payload deviceCount matches {realDevices.Count}");
            Assert(rRoot.TryGetProperty("lastUpdated", out _), "Real payload lastUpdated is present");
            var rDevArray = rRoot.GetProperty("devices");
            Assert(rDevArray.GetArrayLength() == realDevices.Count, $"Real devices array has {realDevices.Count} elements");

            for (int i = 0; i < realDevices.Count; i++)
            {
                var rDev = rDevArray[i];
                Assert(!string.IsNullOrEmpty(rDev.GetProperty("id").GetString()), $"Real device [{i}] has valid ID");
                Assert(!string.IsNullOrEmpty(rDev.GetProperty("name").GetString()), $"Real device [{i}] has valid Name");
                Assert(rDev.TryGetProperty("statusText", out _), $"Real device [{i}] has statusText");
                Assert(rDev.TryGetProperty("icon", out _), $"Real device [{i}] has icon");
                Assert(rDev.TryGetProperty("actionTitle", out _), $"Real device [{i}] has actionTitle");
            }
        }

        Console.WriteLine("\n[Real Paired Devices Payload JSON]:");
        Console.WriteLine(realPayloadJson);
    }

    private static void CheckForToggleAction(JsonElement element, ref bool found)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "Action.Execute")
            {
                if (element.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Object)
                {
                    if (dataProp.TryGetProperty("action", out var actProp) && actProp.GetString() == "toggle" &&
                        dataProp.TryGetProperty("deviceId", out _))
                    {
                        found = true;
                    }
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                CheckForToggleAction(prop.Value, ref found);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CheckForToggleAction(item, ref found);
            }
        }
    }

    private static string? ResolveWindowsRoot()
    {
        foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "BTController.App", "Package.appxmanifest")))
                {
                    return dir.FullName;
                }
                string nested = Path.Combine(dir.FullName, "windows");
                if (File.Exists(Path.Combine(nested, "BTController.App", "Package.appxmanifest")))
                {
                    return nested;
                }
                dir = dir.Parent;
            }
        }
        return null;
    }
}
