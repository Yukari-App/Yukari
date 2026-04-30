using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class FiltersDialog : ContentDialog
{
    public FiltersDialogViewModel ViewModel { get; set; }

    public FiltersDialog(FiltersDialogViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel;
        DataContext = ViewModel;
    }
}
