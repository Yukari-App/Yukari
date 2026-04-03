using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Yukari.Views.Pages;

namespace Yukari
{
    public sealed partial class MainWindow : Window
    {
        private const int MinWidth = 656;
        private const int MinHeight = 500;

        private double _scaleFactor;

        private Frame _rootFrame = new() { Content = new SplashPage() };

        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon("Assets/AppIcon.ico");

            OverlappedPresenter presenter = OverlappedPresenter.Create();
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
            AppWindow.SetPresenter(presenter);

            _scaleFactor = GetScaleFactor();
            AppWindow.Resize(new SizeInt32((int)(1200 * _scaleFactor), (int)(700 * _scaleFactor)));

            Content = _rootFrame;
        }

        public void SetMicaBackdrop() => SystemBackdrop = new MicaBackdrop();

        public void NavigateToShell() =>
            _rootFrame.Navigate(typeof(ShellPage), null, new DrillInNavigationTransitionInfo());

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        private double GetScaleFactor()
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            return GetDpiForWindow(hwnd) / 96.0;
        }
    }
}
