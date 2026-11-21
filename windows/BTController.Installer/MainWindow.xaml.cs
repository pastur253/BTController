using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace BTController.Installer;

public partial class MainWindow : Window
{
    private string _targetDirectory = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        // Default path: ProgramFiles or LocalAppData
        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "BTController");

        TxtPath.Text = defaultPath;
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для установки Bluetooth Controller",
            InitialDirectory = Directory.Exists(TxtPath.Text) ? TxtPath.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dialog.ShowDialog() == true)
        {
            TxtPath.Text = Path.Combine(dialog.FolderName, "BTController");
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        _targetDirectory = TxtPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(_targetDirectory))
        {
            MessageBox.Show("Пожалуйста, укажите папку для установки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfigPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnInstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        bool desktopShortcut = ChkDesktopShortcut.IsChecked == true;
        bool startMenuShortcut = ChkStartMenuShortcut.IsChecked == true;
        bool launchAfter = ChkLaunchAfter.IsChecked == true;

        try
        {
            await Task.Run(() => PerformInstallation(_targetDirectory, desktopShortcut, startMenuShortcut));

            ProgressPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            BtnInstall.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnFinish.Visibility = Visibility.Visible;

            if (launchAfter)
            {
                string exePath = Path.Combine(_targetDirectory, "BTController.App.exe");
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            ConfigPanel.Visibility = Visibility.Visible;
            BtnInstall.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            MessageBox.Show($"Ошибка во время установки:\n{ex.Message}", "Ошибка установки", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PerformInstallation(string targetDir, bool desktopShortcut, bool startMenuShortcut)
    {
        UpdateStatus("Создание папки установки...");
        Directory.CreateDirectory(targetDir);

        UpdateStatus("Распаковка файлов приложения...");
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("app.zip"))
        {
            if (stream == null)
            {
                throw new InvalidOperationException("Пакет установки не содержит встроенного архива приложения (app.zip).");
            }

            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                zip.ExtractToDirectory(targetDir, overwriteFiles: true);
            }
        }

        string appExe = Path.Combine(targetDir, "BTController.App.exe");

        UpdateStatus("Создание ярлыков...");
        if (desktopShortcut)
        {
            string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktopDir, "Bluetooth Controller.lnk"), appExe, targetDir);
        }

        if (startMenuShortcut)
        {
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
            CreateShortcut(Path.Combine(startMenuDir, "Bluetooth Controller.lnk"), appExe, targetDir);
        }

        UpdateStatus("Регистрация программы в системе...");
        RegisterProtocol(appExe);
        RegisterUninstaller(targetDir, appExe);
    }

    private void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtSubStatus.Text = message;
        });
    }

    private static void CreateShortcut(string shortcutPath, string targetExePath, string workingDirectory)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return;

            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExePath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Description = "Управление Bluetooth устройствами и виджет для Windows 11";
            shortcut.Save();
        }
        catch
        {
            // Ignore shortcut creation failures
        }
    }

    private static void RegisterProtocol(string appExePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\btcontroller");
            key.SetValue("", "URL:Bluetooth Controller Protocol");
            key.SetValue("URL Protocol", "");

            using var shellKey = key.CreateSubKey(@"shell\open\command");
            shellKey.SetValue("", $"\"{appExePath}\" \"%1\"");
        }
        catch { }
    }

    private static void RegisterUninstaller(string installDir, string appExePath)
    {
        try
        {
            string uninstallScriptPath = Path.Combine(installDir, "uninstall.cmd");
            File.WriteAllText(uninstallScriptPath,
                $"@echo off\r\n" +
                $"taskkill /F /IM BTController.App.exe 2>nul\r\n" +
                $"timeout /t 1 /nobreak >nul\r\n" +
                $"reg delete \"HKCU\\Software\\Classes\\btcontroller\" /f 2>nul\r\n" +
                $"reg delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\BTController\" /f 2>nul\r\n" +
                $"del /f /q \"{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}\\Bluetooth Controller.lnk\" 2>nul\r\n" +
                $"del /f /q \"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs")}\\Bluetooth Controller.lnk\" 2>nul\r\n" +
                $"echo Удаление файлов...\r\n" +
                $"(goto) 2>nul & rmdir /s /q \"{installDir}\"\r\n");

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\BTController");
            key.SetValue("DisplayName", "Bluetooth Controller");
            key.SetValue("DisplayIcon", appExePath);
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "BTController");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("UninstallString", $"cmd.exe /c \"{uninstallScriptPath}\"");
        }
        catch { }
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
