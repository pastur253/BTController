using System.Text.Json;

namespace BTController.Core.Services;

public class WidgetPreferencesService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BTController");

    private static readonly string SettingsFile = Path.Combine(SettingsFolder, "widget_devices.json");

    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private List<string> _cachedDeviceIds = new();
    private bool _isLoaded;

    public event EventHandler? WidgetSettingsChanged;

    public async Task<List<string>> GetWidgetDeviceIdsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (_isLoaded) return [.. _cachedDeviceIds];

            if (File.Exists(SettingsFile))
            {
                var json = await File.ReadAllTextAsync(SettingsFile);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                _cachedDeviceIds = list ?? new List<string>();
            }
            else
            {
                _cachedDeviceIds = new List<string>();
            }

            _isLoaded = true;
            return [.. _cachedDeviceIds];
        }
        catch
        {
            return [.. _cachedDeviceIds];
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task AddDeviceAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        await _fileLock.WaitAsync();
        try
        {
            if (!_cachedDeviceIds.Contains(deviceId))
            {
                _cachedDeviceIds.Add(deviceId);
                await SaveToFileInternalAsync();
                WidgetSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task RemoveDeviceAsync(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        await _fileLock.WaitAsync();
        try
        {
            if (_cachedDeviceIds.Remove(deviceId))
            {
                await SaveToFileInternalAsync();
                WidgetSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task MoveUpAsync(string deviceId)
    {
        await _fileLock.WaitAsync();
        try
        {
            int index = _cachedDeviceIds.IndexOf(deviceId);
            if (index > 0)
            {
                _cachedDeviceIds.RemoveAt(index);
                _cachedDeviceIds.Insert(index - 1, deviceId);
                await SaveToFileInternalAsync();
                WidgetSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task MoveDownAsync(string deviceId)
    {
        await _fileLock.WaitAsync();
        try
        {
            int index = _cachedDeviceIds.IndexOf(deviceId);
            if (index >= 0 && index < _cachedDeviceIds.Count - 1)
            {
                _cachedDeviceIds.RemoveAt(index);
                _cachedDeviceIds.Insert(index + 1, deviceId);
                await SaveToFileInternalAsync();
                WidgetSettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveToFileInternalAsync()
    {
        Directory.CreateDirectory(SettingsFolder);
        var json = JsonSerializer.Serialize(_cachedDeviceIds, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(SettingsFile, json);
    }
}
