using System;
using System.Threading.Tasks;
using BTController.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace BTController.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private string? _downloadUrl;
    private string? _assetFileName;

    [ObservableProperty]
    public partial string AppVersion { get; set; } = $"v{App.UpdateService.GetCurrentVersion()} (Windows 11 WinUI 3)";

    public string DeveloperName => "PasturTheFox";

    public string GitHubUrl => "https://github.com/pastur253/BTController";

    public string BugReportUrl => "https://github.com/pastur253/BTController/issues/new?template=bug_report.md";

    public string FeatureRequestUrl => "https://github.com/pastur253/BTController/issues/new?template=feature_request.md";

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "Нажмите «Проверить» для поиска обновлений на GitHub";

    [ObservableProperty]
    public partial bool IsCheckingUpdate { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsDownloading { get; set; }

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial string? LatestVersion { get; set; }

    public SettingsViewModel()
    {
        if (App.UpdateService.LastResult is { } lastResult)
        {
            ApplyCheckResult(lastResult);
        }
    }

    public bool CanCheckUpdate => !IsCheckingUpdate && !IsDownloading;

    public Visibility DownloadButtonVisibility => IsUpdateAvailable && !IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DownloadProgressVisibility => IsDownloading ? Visibility.Visible : Visibility.Collapsed;

    public string DownloadButtonText => string.IsNullOrWhiteSpace(LatestVersion)
        ? "Обновить"
        : $"Обновить до v{LatestVersion}";

    partial void OnIsCheckingUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckUpdate));
    }

    partial void OnIsDownloadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckUpdate));
        OnPropertyChanged(nameof(DownloadButtonVisibility));
        OnPropertyChanged(nameof(DownloadProgressVisibility));
    }

    partial void OnIsUpdateAvailableChanged(bool value)
    {
        OnPropertyChanged(nameof(DownloadButtonVisibility));
    }

    partial void OnLatestVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(DownloadButtonText));
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        if (IsCheckingUpdate || IsDownloading) return;

        IsCheckingUpdate = true;
        IsUpdateAvailable = false;
        UpdateStatus = "Проверка обновлений на GitHub...";

        try
        {
            var result = await App.UpdateService.CheckForUpdateAsync();
            ApplyCheckResult(result);
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    public void ApplyCheckResult(UpdateCheckResult result)
    {
        if (!result.IsSuccess)
        {
            IsUpdateAvailable = false;
            UpdateStatus = result.ErrorMessage ?? "Не удалось проверить обновления.";
            return;
        }

        if (result.IsUpdateAvailable)
        {
            LatestVersion = result.LatestVersion;
            _downloadUrl = result.DownloadUrl;
            _assetFileName = result.AssetFileName;
            IsUpdateAvailable = !string.IsNullOrWhiteSpace(_downloadUrl);
            UpdateStatus = IsUpdateAvailable
                ? $"Доступна новая версия v{result.LatestVersion} (текущая: v{result.CurrentVersion})."
                : $"Доступна версия v{result.LatestVersion}, но файл установки не найден в релизе.";
        }
        else
        {
            IsUpdateAvailable = false;
            LatestVersion = result.LatestVersion;
            UpdateStatus = $"Установлена актуальная версия (v{result.CurrentVersion}).";
        }
    }

    [RelayCommand]
    public async Task DownloadAndInstallAsync()
    {
        if (IsDownloading || string.IsNullOrWhiteSpace(_downloadUrl)) return;

        IsDownloading = true;
        DownloadProgress = 0;
        UpdateStatus = $"Загрузка обновления v{LatestVersion}...";

        try
        {
            var progress = new Progress<double>(percent =>
            {
                DownloadProgress = percent;
                UpdateStatus = $"Загрузка обновления v{LatestVersion}: {percent:0}%";
            });

            string installerPath = await App.UpdateService.DownloadInstallerAsync(
                _downloadUrl,
                _assetFileName ?? "BTController-Setup.exe",
                progress);

            UpdateStatus = "Запуск установщика...";
            App.UpdateService.LaunchInstaller(installerPath);
            UpdateStatus = "Установщик запущен. Следуйте инструкциям на экране.";
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Ошибка при загрузке обновления: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }
}
