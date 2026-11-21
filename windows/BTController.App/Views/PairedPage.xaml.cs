using BTController.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace BTController.App.Views;

public sealed partial class PairedPage : Page
{
    public PairedViewModel ViewModel { get; } = new();

    public PairedPage()
    {
        this.InitializeComponent();
    }
}
