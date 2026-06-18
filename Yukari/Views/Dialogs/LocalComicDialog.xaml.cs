using Microsoft.UI.Xaml.Controls;
using Yukari.Models.DTO;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class LocalComicDialog : ContentDialog
{
    public LocalComicDialogViewModel ViewModel { get; set; }

    public LocalComicDialog(ContentKey? comicKey)
    {
        InitializeComponent();
        ViewModel = App.GetService<LocalComicDialogViewModel>();
        DataContext = ViewModel;

        _ = ViewModel.InitializeAsync(comicKey);
    }
}
