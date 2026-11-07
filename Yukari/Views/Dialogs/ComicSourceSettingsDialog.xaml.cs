using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class ComicSourceSettingsDialog : ContentDialog
{
    public ComicSourceSettingsDialogViewModel ViewModel { get; set; }

    public ComicSourceSettingsDialog(string sourceName)
    {
        InitializeComponent();
        ViewModel = App.GetService<ComicSourceSettingsDialogViewModel>();
        DataContext = ViewModel;

        _ = ViewModel.InitializeAsync(sourceName);
    }

    private void ComicSourceSettingsDialog_SecondaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        args.Cancel = true;
        ViewModel.RestoreDefaultsCommand.Execute(null);
    }
}
