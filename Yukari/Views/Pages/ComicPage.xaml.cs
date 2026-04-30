using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Yukari.Models.DTO;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages;

public sealed partial class ComicPage : Page
{
    public ComicPageViewModel ViewModel { get; set; }

    public ComicPage()
    {
        InitializeComponent();

        ViewModel = App.GetService<ComicPageViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ContentKey ComicKey)
        {
            await ViewModel.InitializeAsync(ComicKey);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }
}
