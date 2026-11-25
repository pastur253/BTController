using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BTController.Core.Services;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace BTController.Widgets;

[Guid("B7E209E3-4028-4822-8356-9A0191B95B11")]
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public class BluetoothWidgetProvider : IWidgetProvider, IWidgetProvider2
{
    public static readonly Guid ProviderClsid = new("B7E209E3-4028-4822-8356-9A0191B95B11");

    private sealed record WidgetEntry(WidgetContext Context, bool IsActive);

    private static readonly ConcurrentDictionary<string, WidgetEntry> Widgets = new();
    private static readonly object InitSync = new();
    private static IBluetoothService? _bluetoothService;
    private static WidgetPreferencesService? _widgetPrefs;

    private static string? _templateMedium;
    private static string? _templateLarge;

    public BluetoothWidgetProvider()
    {
        EnsureServicesInitialized();
        RecoverExistingWidgets();
    }

    private static void EnsureServicesInitialized()
    {
        if (_bluetoothService != null && _widgetPrefs != null)
        {
            return;
        }

        lock (InitSync)
        {
            if (_bluetoothService != null && _widgetPrefs != null)
            {
                return;
            }

            var prefs = new WidgetPreferencesService();
            var btService = new WindowsBluetoothService(prefs);
            btService.DeviceUpdated += OnBluetoothChanged;
            btService.DevicesReloaded += OnBluetoothChanged;

            _widgetPrefs = prefs;
            _bluetoothService = btService;
            _ = InitializeBluetoothSafeAsync(btService);
        }
    }

    private static async Task InitializeBluetoothSafeAsync(IBluetoothService btService)
    {
        try
        {
            await btService.InitializeAsync();
        }
        catch (Exception ex)
        {
            LogError("InitializeBluetoothSafeAsync", ex);
        }
    }

    private static void OnBluetoothChanged(object? sender, EventArgs e)
    {
        _ = UpdateAllActiveWidgetsAsync();
    }

    private static void OnBluetoothChanged(object? sender, Core.Models.BluetoothDeviceModel e)
    {
        _ = UpdateAllActiveWidgetsAsync();
    }

    private static void RecoverExistingWidgets()
    {
        try
        {
            var widgetInfos = WidgetManager.GetDefault().GetWidgetInfos();
            if (widgetInfos == null)
            {
                return;
            }

            foreach (var info in widgetInfos)
            {
                var ctx = info.WidgetContext;
                if (ctx != null)
                {
                    Widgets[ctx.Id] = new WidgetEntry(ctx, info.WidgetContext.IsActive);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("RecoverExistingWidgets", ex);
        }
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        Widgets[widgetContext.Id] = new WidgetEntry(widgetContext, true);
        _ = UpdateWidgetAsync(widgetContext);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        Widgets.TryRemove(widgetId, out _);
    }

    public void Activate(WidgetContext widgetContext)
    {
        Widgets[widgetContext.Id] = new WidgetEntry(widgetContext, true);
        _ = UpdateWidgetAsync(widgetContext);
    }

    public void Deactivate(string widgetId)
    {
        if (Widgets.TryGetValue(widgetId, out var existing))
        {
            Widgets[widgetId] = existing with { IsActive = false };
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        var ctx = contextChangedArgs.WidgetContext;
        bool isActive = Widgets.TryGetValue(ctx.Id, out var existing) ? existing.IsActive : ctx.IsActive;
        Widgets[ctx.Id] = new WidgetEntry(ctx, isActive);
        _ = UpdateWidgetAsync(ctx);
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs customizationRequestedArgs)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "btcontroller://settings",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogError("OnCustomizationRequested", ex);
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = actionInvokedArgs.Verb;
        var data = actionInvokedArgs.Data;
        var widgetContext = actionInvokedArgs.WidgetContext;

        _ = HandleActionAsync(widgetContext, verb, data);
    }

    private async Task HandleActionAsync(WidgetContext context, string verb, string data)
    {
        try
        {
            EnsureServicesInitialized();

            string effectiveVerb = verb;
            if (string.IsNullOrEmpty(effectiveVerb) && !string.IsNullOrWhiteSpace(data))
            {
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    if (doc.RootElement.TryGetProperty("action", out var actProp))
                    {
                        effectiveVerb = actProp.GetString() ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    LogError("HandleActionAsync.ParseVerb", ex);
                }
            }

            switch (effectiveVerb)
            {
                case "toggle":
                case "toggleConnection":
                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        using var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("deviceId", out var devProp))
                        {
                            string deviceId = devProp.GetString() ?? string.Empty;
                            if (_bluetoothService != null && !string.IsNullOrEmpty(deviceId))
                            {
                                await _bluetoothService.ToggleConnectionAsync(deviceId);
                            }
                        }
                    }
                    break;

                case "refresh":
                    if (_bluetoothService != null)
                    {
                        await _bluetoothService.InitializeAsync();
                    }
                    break;

                case "openSettings":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "btcontroller://settings",
                        UseShellExecute = true
                    });
                    break;

                case "openApp":
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "btcontroller://",
                        UseShellExecute = true
                    });
                    break;
            }

            await UpdateWidgetAsync(context);
        }
        catch (Exception ex)
        {
            LogError("HandleActionAsync", ex);
        }
    }

    private static async Task UpdateAllActiveWidgetsAsync()
    {
        try
        {
            foreach (var entry in Widgets.Values.ToArray())
            {
                if (entry.IsActive)
                {
                    await UpdateWidgetAsync(entry.Context);
                }
            }
        }
        catch (Exception ex)
        {
            LogError("UpdateAllActiveWidgetsAsync", ex);
        }
    }

    private static async Task UpdateWidgetAsync(WidgetContext context)
    {
        try
        {
            EnsureServicesInitialized();
            if (_bluetoothService == null) return;

            var devices = await _bluetoothService.GetWidgetDevicesAsync();
            string dataPayload = WidgetPayloadHelper.CreateDataPayload(devices);

            string template = GetTemplate(context.Size);

            var updateOptions = new WidgetUpdateRequestOptions(context.Id)
            {
                Template = template,
                Data = dataPayload
            };

            WidgetManager.GetDefault().UpdateWidget(updateOptions);
        }
        catch (Exception ex)
        {
            LogError("UpdateWidgetAsync", ex);
        }
    }

    private static string GetTemplate(WidgetSize size)
    {
        if (size == WidgetSize.Large)
        {
            return _templateLarge ??= LoadTemplateFile("WidgetTemplateLarge.json");
        }

        return _templateMedium ??= LoadTemplateFile("WidgetTemplateMedium.json");
    }

    private static string LoadTemplateFile(string filename)
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Templates", filename),
            Path.Combine(AppContext.BaseDirectory, "BTController.Widgets", "Templates", filename)
        ];

        foreach (string path in candidates)
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        LogError("LoadTemplateFile", new FileNotFoundException($"Widget template file not found: {candidates[0]}", candidates[0]));
        return "{}";
    }

    private static void LogError(string source, Exception ex)
    {
        Debug.WriteLine($"[WidgetProvider] {source}: {ex.Message}");
        try
        {
            string logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "BTController");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "widget.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex.GetType().FullName}: {ex.Message}\r\n{ex.StackTrace}\r\n");
        }
        catch
        {
            // Logging failure must not crash the COM server
        }
    }
}
