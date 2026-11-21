using System;
using System.IO;
using System.Threading.Tasks;
using BTController.Core.Services;
using Microsoft.UI.Xaml;

namespace BTController.App;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public static WidgetPreferencesService WidgetPreferences { get; } = new();
    public static IBluetoothService BluetoothService { get; } = new WindowsBluetoothService(WidgetPreferences);

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogCrash("TaskScheduler.UnobservedTaskException", e.Exception);
        };

        UnhandledException += (s, e) =>
        {
            LogCrash("Application.UnhandledException", e.Exception);
        };

        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _ = BluetoothService.InitializeAsync();

            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("App.OnLaunched", ex);
            throw;
        }
    }

    public static void LogCrash(string source, Exception? ex)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string logDir = Path.Combine(appData, "BTController");
            Directory.CreateDirectory(logDir);
            string logFile = Path.Combine(logDir, "crash.log");

            string message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{source}] {ex?.GetType().FullName}: {ex?.Message}\r\nStack Trace:\r\n{ex?.StackTrace}\r\n";
            if (ex?.InnerException != null)
            {
                message += $"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\r\n{ex.InnerException.StackTrace}\r\n";
            }
            message += new string('-', 80) + "\r\n";

            File.AppendAllText(logFile, message);
        }
        catch
        {
            // Logging failure should never crash the process
        }
    }
}
