using BTController.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BTController.App.Views;

public sealed partial class DiscoveryPage : Page
{
    public DiscoveryViewModel ViewModel { get; } = new();

    public DiscoveryPage()
    {
        this.InitializeComponent();
    }

    public Visibility GetScanButtonVisibility(bool isScanning) =>
        isScanning ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GetStopButtonVisibility(bool isScanning) =>
        isScanning ? Visibility.Visible : Visibility.Collapsed;
}
