using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class LibraryExportDialog : ContentDialog
{
    public LibraryExportDialogViewModel ViewModel { get; set; }

    public LibraryExportDialog()
    {
        InitializeComponent();

        ViewModel = App.GetService<LibraryExportDialogViewModel>();
        DataContext = ViewModel;
    }

    private async void LibraryExportDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        args.Cancel = true;
        await ViewModel.ExportCommand.ExecuteAsync(null);
    }
}
