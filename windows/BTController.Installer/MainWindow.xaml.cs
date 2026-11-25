using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;

namespace BTController.Installer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        ConfigPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        BtnInstall.IsEnabled = false;
        BtnCancel.IsEnabled = false;

        bool launchAfter = ChkLaunchAfter.IsChecked == true;

        try
        {
            await Task.Run(PerformInstallation);

            ProgressPanel.Visibility = Visibility.Collapsed;
            SuccessPanel.Visibility = Visibility.Visible;
            BtnInstall.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnFinish.Visibility = Visibility.Visible;

            if (launchAfter)
            {
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
                    // App is installed in Start Menu even if protocol launch fails
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

    private void PerformInstallation()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"BTController-Setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            UpdateStatus("Распаковка пакета установки...");
            string cerPath = Path.Combine(tempDir, "BTController-Windows-x64.cer");
            string msixPath = Path.Combine(tempDir, "BTController-Windows-x64.msix");

            ExtractEmbeddedResource("BTController.cer", cerPath);
            ExtractEmbeddedResource("BTController.msix", msixPath);

            UpdateStatus("Проверка и установка сертификата подписи...");
            EnsureCertificateTrusted(cerPath);

            UpdateStatus("Установка MSIX-пакета Bluetooth Controller...");
            InstallMsixPackage(msixPath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignore temp cleanup errors
            }
        }
    }

    private static void ExtractEmbeddedResource(string resourceName, string destinationPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"В установщике отсутствует встроенный ресурс '{resourceName}'.");
        using var fileStream = File.Create(destinationPath);
        stream.CopyTo(fileStream);
    }

    private static void EnsureCertificateTrusted(string cerPath)
    {
        using var cert = new X509Certificate2(cerPath);
        if (IsCertificateInStore(cert.Thumbprint, StoreName.TrustedPeople, StoreLocation.LocalMachine) ||
            IsCertificateInStore(cert.Thumbprint, StoreName.Root, StoreLocation.LocalMachine))
        {
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "certutil.exe",
                Arguments = $"-addstore TrustedPeople \"{cerPath}\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Не удалось запустить certutil.exe для установки сертификата.");
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                throw new InvalidOperationException($"certutil.exe завершился с кодом {proc.ExitCode}.");
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("Установка сертификата отменена пользователем (UAC).", ex);
        }
    }

    private static bool IsCertificateInStore(string thumbprint, StoreName storeName, StoreLocation location)
    {
        try
        {
            using var store = new X509Store(storeName, location);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void InstallMsixPackage(string msixPath)
    {
        string escapedPath = msixPath.Replace("'", "''");
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path '{escapedPath}' -ForceApplicationShutdown\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить PowerShell для установки MSIX-пакета.");
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            string details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException($"Add-AppxPackage завершился с кодом {proc.ExitCode}:\n{details.Trim()}");
        }
    }

    private void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtSubStatus.Text = message;
        });
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
}
