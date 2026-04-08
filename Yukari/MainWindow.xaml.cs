using System;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Yukari.Messages;
using Yukari.Views.Pages;

namespace Yukari
{
    public sealed partial class MainWindow : Window, IRecipient<SetFullscreenMessage>
    {
        private const string WindowTitle = "Yukari";
        private const string WindowIconPath = "Assets/AppIcon.ico";

        private const int MinWidth = 656;
        private const int MinHeight = 500;
        private const int DefaultWidth = 1200;
        private const int DefaultHeight = 700;

        private readonly IMessenger _messenger;

        private readonly double _scaleFactor;
        private readonly OverlappedPresenter _presenter;

        private Frame _rootFrame = new() { Content = new SplashPage() };

        public MainWindow()
        {
            InitializeComponent();
            _messenger = App.GetService<IMessenger>();

            Title = WindowTitle;
            AppWindow.SetIcon(WindowIconPath);
            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            _presenter = OverlappedPresenter.Create();
            _presenter.PreferredMinimumWidth = MinWidth;
            _presenter.PreferredMinimumHeight = MinHeight;
            AppWindow.SetPresenter(_presenter);

            _scaleFactor = GetScaleFactor();
            AppWindow.Resize(new SizeInt32((int)(DefaultWidth * _scaleFactor), (int)(DefaultHeight * _scaleFactor)));

            Content = _rootFrame;
            _messenger.RegisterAll(this);
        }

        public void Receive(SetFullscreenMessage message) =>
            SetFullscreenState(message.IsFullscreen);

        public void SetMicaBackdrop() => SystemBackdrop = new MicaBackdrop();

        public void NavigateToShell() =>
            _rootFrame.Navigate(typeof(ShellPage), null, new DrillInNavigationTransitionInfo());

        private void SetFullscreenState(bool isFullscreen)
        {
            if (isFullscreen)
                AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            else
                AppWindow.SetPresenter(_presenter);
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        private double GetScaleFactor()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            return GetDpiForWindow(hwnd) / 96.0;
        }
    }
}
