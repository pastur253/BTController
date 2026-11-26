using System;
using System.IO;
using System.Threading.Tasks;
using BTController.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace BTController.App;

public static class CrashLogger
{
    public static void Log(string source, Exception? ex)
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

public partial class App : Application
{
    private static readonly Lazy<WidgetPreferencesService> LazyWidgetPreferences = new(() => new WidgetPreferencesService());
    private static readonly Lazy<IBluetoothService> LazyBluetoothService = new(() => new WindowsBluetoothService(WidgetPreferences));
    private static readonly Lazy<Services.UpdateService> LazyUpdateService = new(() => new Services.UpdateService());

    public static MainWindow? MainWindow { get; private set; }
    public static WidgetPreferencesService WidgetPreferences => LazyWidgetPreferences.Value;
    public static IBluetoothService BluetoothService => LazyBluetoothService.Value;
    public static Services.UpdateService UpdateService => LazyUpdateService.Value;

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

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            _ = BluetoothService.InitializeAsync();

            MainWindow = new MainWindow();
            ApplyActivationRoute(AppInstance.GetCurrent().GetActivatedEventArgs());
            MainWindow.Activate();
        }
        catch (Exception ex)
        {
            LogCrash("App.OnLaunched", ex);
            throw;
        }
    }

    public void HandleRedirectedActivation(AppActivationArguments activationArgs)
    {
        if (MainWindow == null) return;

        MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            ApplyActivationRoute(activationArgs);
            MainWindow.Activate();
        });
    }

    private static void ApplyActivationRoute(AppActivationArguments? activationArgs)
    {
        if (MainWindow == null || activationArgs == null) return;

        if (activationArgs.Kind == ExtendedActivationKind.Protocol &&
            activationArgs.Data is IProtocolActivatedEventArgs protocolArgs)
        {
            MainWindow.NavigateToRoute(protocolArgs.Uri?.Host);
        }
    }

    public static void LogCrash(string source, Exception? ex) => CrashLogger.Log(source, ex);
}
