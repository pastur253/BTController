using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace BTController.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string AppVersion { get; set; } = "1.0.0 (Windows 11 WinUI 3)";

    [ObservableProperty]
    public partial string UpdateStatus { get; set; } = "У вас установлена актуальная версия";

    [ObservableProperty]
    public partial bool IsCheckingUpdate { get; set; }

    public bool CanCheckUpdate => !IsCheckingUpdate;

    partial void OnIsCheckingUpdateChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCheckUpdate));
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        UpdateStatus = "Проверка обновлений на GitHub...";
        await Task.Delay(1500); // Simulate check
        IsCheckingUpdate = false;
        UpdateStatus = "Установлена последняя версия приложения.";
    }
}
