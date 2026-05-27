using Microsoft.UI.Xaml.Controls;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class CollectionsManagerDialog : ContentDialog
{
    public CollectionsManagerDialogViewModel ViewModel { get; set; }

    public CollectionsManagerDialog()
    {
        InitializeComponent();

        ViewModel = App.GetService<CollectionsManagerDialogViewModel>();
        DataContext = ViewModel;
    }
}
