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
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = BluetoothService.InitializeAsync();

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
