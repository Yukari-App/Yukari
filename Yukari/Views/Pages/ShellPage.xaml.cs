using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.ViewModels.Pages;

namespace Yukari.Views.Pages
{
    public sealed partial class ShellPage : Page
    {
        public ShellPageViewModel ViewModel { get; set; }

        public ShellPage()
        {
            InitializeComponent();
            ViewModel = App.GetService<ShellPageViewModel>();
            DataContext = ViewModel;

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
}
