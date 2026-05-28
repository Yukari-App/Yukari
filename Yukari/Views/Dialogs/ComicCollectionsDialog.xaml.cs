using Microsoft.UI.Xaml.Controls;
using Yukari.Models.DTO;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class ComicCollectionsDialog : ContentDialog
{
    public ComicCollectionsDialogViewModel ViewModel { get; set; }

    public ComicCollectionsDialog(ContentKey comicKey, string comicTitle)
    {
        InitializeComponent();
        ViewModel = App.GetService<ComicCollectionsDialogViewModel>();
        DataContext = ViewModel;

        _ = ViewModel.InitializeAsync(comicKey, comicTitle);
    }
}
