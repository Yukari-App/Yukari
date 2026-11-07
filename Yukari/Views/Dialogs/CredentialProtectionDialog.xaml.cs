using Microsoft.UI.Xaml.Controls;
using Yukari.Enums;
using Yukari.ViewModels.Dialogs;

namespace Yukari.Views.Dialogs;

public sealed partial class CredentialProtectionDialog : ContentDialog
{
    public CredentialProtectionDialogViewModel ViewModel { get; }

    public CredentialProtectionDialog(CredentialProtectionMode mode)
    {
        InitializeComponent();
        ViewModel = App.GetService<CredentialProtectionDialogViewModel>();
        DataContext = ViewModel;
        ViewModel.Initialize(mode);
    }

    private async void CredentialProtectionDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args
    )
    {
        var deferral = args.GetDeferral();
        try
        {
            var success = await ViewModel.SubmitAsync();
            if (!success)
                args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
