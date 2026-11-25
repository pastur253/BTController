using BTController.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BTController.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();
        
        // Default navigation
        NavView.SelectedItem = NavView.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
    }

    public void NavigateToRoute(string? route)
    {
        switch (route?.ToLowerInvariant())
        {
            case "settings":
                NavView.SelectedItem = NavView.SettingsItem;
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
            case "paired":
                NavView.SelectedItem = NavView.MenuItems[1];
                ContentFrame.Navigate(typeof(PairedPage));
                break;
            case "discovery":
                NavView.SelectedItem = NavView.MenuItems[2];
                ContentFrame.Navigate(typeof(DiscoveryPage));
                break;
            default:
                NavView.SelectedItem = NavView.MenuItems[0];
                ContentFrame.Navigate(typeof(DashboardPage));
                break;
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "DashboardPage":
                    ContentFrame.Navigate(typeof(DashboardPage));
                    break;
                case "PairedPage":
                    ContentFrame.Navigate(typeof(PairedPage));
                    break;
                case "DiscoveryPage":
                    ContentFrame.Navigate(typeof(DiscoveryPage));
                    break;
            }
        }
    }
}
