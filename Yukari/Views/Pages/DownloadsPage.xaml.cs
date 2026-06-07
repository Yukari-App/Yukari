using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages;

public sealed partial class DownloadsPage : Page
{
    public DownloadsPageViewModel ViewModel { get; set; }

    public DownloadsPage()
    {
        InitializeComponent();

        ViewModel = App.GetService<DownloadsPageViewModel>();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }

    private void CancelOrRetryItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DownloadItem item)
            ViewModel.CancelOrRetryItemClickCommand.Execute(item);
    }

    private void DeleteItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.CommandParameter is DownloadItem item)
            ViewModel.DeleteItemClickCommand.Execute(item);
    }
}
