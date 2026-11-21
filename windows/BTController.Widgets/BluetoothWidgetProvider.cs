using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BTController.Core.Services;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace BTController.Widgets;

[Guid("B7E209E3-4028-4822-8356-9A0191B95B11")]
[ComVisible(true)]
public class BluetoothWidgetProvider : IWidgetProvider, IWidgetProvider2
{
    public static readonly Guid ProviderClsid = new("B7E209E3-4028-4822-8356-9A0191B95B11");

    private static readonly Dictionary<string, WidgetContext> ActiveWidgets = new();
    private static IBluetoothService? _bluetoothService;
    private static WidgetPreferencesService? _widgetPrefs;

    private static string? _templateMedium;
    private static string? _templateLarge;

    public BluetoothWidgetProvider()
    {
        EnsureServicesInitialized();
    }

    private static void EnsureServicesInitialized()
    {
        if (_widgetPrefs == null)
        {
            _widgetPrefs = new WidgetPreferencesService();
            _bluetoothService = new WindowsBluetoothService(_widgetPrefs);
            _bluetoothService.DeviceUpdated += async (s, dev) => await UpdateAllActiveWidgetsAsync();
            _bluetoothService.DevicesReloaded += async (s, e) => await UpdateAllActiveWidgetsAsync();
            _ = _bluetoothService.InitializeAsync();
        }
    }

    public void CreateWidget(WidgetContext widgetContext)
    {
        ActiveWidgets[widgetContext.Id] = widgetContext;
        _ = UpdateWidgetAsync(widgetContext);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        ActiveWidgets.Remove(widgetId);
    }

    public void Activate(WidgetContext widgetContext)
    {
        ActiveWidgets[widgetContext.Id] = widgetContext;
        _ = UpdateWidgetAsync(widgetContext);
    }

    public void Deactivate(string widgetId)
    {
        // Widget went out of view
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        var ctx = contextChangedArgs.WidgetContext;
        ActiveWidgets[ctx.Id] = ctx;
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
        catch
        {
            // Fallback
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

            switch (verb)
            {
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
                case "openApp":
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "btcontroller://",
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                        // Fallback
                    }
                    break;
            }

            await UpdateWidgetAsync(context);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WidgetProvider] Action error: {ex.Message}");
        }
    }

    private static async Task UpdateAllActiveWidgetsAsync()
    {
        foreach (var ctx in ActiveWidgets.Values.ToList())
        {
            await UpdateWidgetAsync(ctx);
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
            Debug.WriteLine($"[WidgetProvider] UpdateWidget error: {ex.Message}");
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
        string localPath = Path.Combine(AppContext.BaseDirectory, "Templates", filename);
        if (File.Exists(localPath))
        {
            return File.ReadAllText(localPath);
        }

        return "{}";
    }
}
