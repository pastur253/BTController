using BTController.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace BTController.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = new();

    public DashboardPage()
    {
        this.InitializeComponent();
    }
}
