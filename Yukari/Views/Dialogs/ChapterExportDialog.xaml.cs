using Microsoft.UI.Xaml.Controls;
using Yukari.Models.DTO;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class ChapterExportDialog : ContentDialog
{
    public ChapterExportDialogViewModel ViewModel { get; set; }

    public ChapterExportDialog(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle
    )
    {
        InitializeComponent();

        ViewModel = App.GetService<ChapterExportDialogViewModel>();
        DataContext = ViewModel;

        ViewModel.Initialize(comicKey, chapterKey, comicTitle, chapterTitle);
    }

    private async void ChapterExportDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        args.Cancel = true;
        await ViewModel.ExportCommand.ExecuteAsync(null);
    }
}
