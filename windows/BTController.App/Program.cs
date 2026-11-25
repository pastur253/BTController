using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BTController.Widgets;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT;

namespace BTController.App;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            CrashLogger.Log("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            CrashLogger.Log("TaskScheduler.UnobservedTaskException", e.Exception);
        };

        try
        {
            if (IsComServerActivation(args))
            {
                RunComWidgetServer();
                return;
            }

            if (RedirectToExistingGuiInstance())
            {
                return;
            }

            ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                var app = new App();
                AppInstance.GetCurrent().Activated += (sender, activationArgs) =>
                {
                    app.HandleRedirectedActivation(activationArgs);
                };
            });
        }
        catch (Exception ex)
        {
            CrashLogger.Log("Program.Main", ex);
            throw;
        }
    }

    private static bool IsComServerActivation(string[] args)
    {
        return args.Any(a =>
            string.Equals(a, "-RegisterProcessAsComServer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "-Embedding", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RedirectToExistingGuiInstance()
    {
        try
        {
            var mainInstance = AppInstance.FindOrRegisterForKey("main");
            if (!mainInstance.IsCurrent)
            {
                var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
                mainInstance.RedirectActivationToAsync(activationArgs).AsTask().GetAwaiter().GetResult();
                return true;
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log("Program.RedirectToExistingGuiInstance", ex);
        }

        return false;
    }

    private static void RunComWidgetServer()
    {
        using var shutdownEvent = new ManualResetEventSlim(false);
        Exception? threadException = null;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdownEvent.Set();

        var comThread = new Thread(() =>
        {
            uint cookie = 0;
            try
            {
                ComWrappersSupport.InitializeComWrappers();
                var factory = new WidgetProviderFactory<BluetoothWidgetProvider>();
                cookie = ComClassObject.Register(BluetoothWidgetProvider.ProviderClsid, factory);
                shutdownEvent.Wait();
            }
            catch (Exception ex)
            {
                threadException = ex;
                CrashLogger.Log("Program.RunComWidgetServer", ex);
            }
            finally
            {
                try
                {
                    ComClassObject.Revoke(cookie);
                }
                catch (Exception ex)
                {
                    CrashLogger.Log("Program.ComRevoke", ex);
                }
            }
        });

        comThread.IsBackground = false;
        comThread.SetApartmentState(ApartmentState.MTA);
        comThread.Start();
        comThread.Join();

        if (threadException != null)
        {
            throw threadException;
        }
    }
}
