using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class LibraryImportDialog : ContentDialog
{
    public LibraryImportDialogViewModel ViewModel { get; set; }

    public LibraryImportDialog()
    {
        InitializeComponent();

        ViewModel = App.GetService<LibraryImportDialogViewModel>();
        DataContext = ViewModel;
    }

    private async void LibraryImportDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        args.Cancel = true;
        await ViewModel.ImportCommand.ExecuteAsync(null);
    }
}
