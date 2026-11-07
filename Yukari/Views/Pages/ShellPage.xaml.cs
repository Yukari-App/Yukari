using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Services.Security;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages;

public sealed partial class ShellPage : Page
{
    private static bool _hasPromptedCredentialUnlock;

    public ShellPageViewModel ViewModel { get; set; }

    public ShellPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<ShellPageViewModel>();
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            var dialogService = App.GetService<IDialogService>();
            dialogService.Initialize(XamlRoot);

            if (!_hasPromptedCredentialUnlock)
            {
                _hasPromptedCredentialUnlock = true;
                var credentialProtector = App.GetService<ICredentialProtector>();
                if (credentialProtector.UsesEntropy && !credentialProtector.IsUnlocked)
                    await dialogService.ShowCredentialProtectionDialogAsync(
                        CredentialProtectionMode.Unlock
                    );
            }
        };

        var messenger = App.GetService<IMessenger>();

        messenger.Register<SwitchAppModeMessage>(
            this,
            (r, m) =>
            {
                var pageType =
                    m.appMode == AppMode.Reader ? typeof(ReaderPage) : typeof(NavigationPage);
                var transitionInfo =
                    m.appMode == AppMode.Reader
                        ? new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromRight,
                        }
                        : new SlideNavigationTransitionInfo()
                        {
                            Effect = SlideNavigationTransitionEffect.FromLeft,
                        };

                ShellFrame.Navigate(pageType, m.Parameter, transitionInfo);
            }
        );
    }
}
