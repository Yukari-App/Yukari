using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs
{
    public sealed partial class FiltersDialog : ContentDialog
    {
        public FiltersDialog(FiltersDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
