using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.Resources;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPageViewModel ViewModel { get; set; }

    public SettingsPage()
    {
        InitializeComponent();

        ViewModel = App.GetService<SettingsPageViewModel>();
        DataContext = ViewModel;

        var loader = ResourceLoader.GetForViewIndependentUse();
        AboutExpander.Description = string.Format(
            loader.GetString("About/Copyright"),
            "2026",
            "TXG0Fk3",
            "GPL-3.0"
        );
        LicenseHLBT.Content = string.Format(loader.GetString("License"), "GPL-3.0");
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        await ViewModel.OnNavigatedFromAsync();
    }
}
