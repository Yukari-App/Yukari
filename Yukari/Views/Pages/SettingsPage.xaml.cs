using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Services.UI;
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

        var lclService = App.GetService<ILocalizationService>();
        AboutExpander.Description = lclService.GetFormattedString(
            "About/Copyright",
            "2026",
            "TXG0Fk3",
            "GPL-3.0"
        );
        LicenseHLBT.Content = lclService.GetFormattedString("License", "GPL-3.0");
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        await ViewModel.OnNavigatedFromAsync();
    }
}
