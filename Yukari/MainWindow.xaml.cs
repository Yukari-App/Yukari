using System;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Serilog;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Yukari.Core.Sources.Browser;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Messages.Shortcuts;
using Yukari.Models.DTO;
using Yukari.Models.Settings;
using Yukari.Services.Settings;
using Yukari.Views.Pages;

namespace Yukari;

public sealed partial class MainWindow : Window, IBrowserHost, IRecipient<SetFullscreenMessage>
{
    private const string WindowTitle = "Yukari";
    private const string WindowIconPath = "Assets/AppIcon.ico";

    private const int MinWidth = 654;
    private const int MinHeight = 500;

    private readonly IMessenger _messenger;
    private readonly ISettingsService _settingsService;

    private readonly double _scaleFactor;
    private readonly OverlappedPresenter _presenter;

    private Storyboard? _currentStoryboard;

    public event Action? UserCancelledInteraction;

    public MainWindow()
    {
        InitializeComponent();
        _messenger = App.GetService<IMessenger>();
        _settingsService = App.GetService<ISettingsService>();

        Title = WindowTitle;
        AppWindow.SetIcon(WindowIconPath);
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        SetTheme(_settingsService.Current.Theme);

        _presenter = OverlappedPresenter.Create();
        _presenter.PreferredMinimumWidth = MinWidth;
        _presenter.PreferredMinimumHeight = MinHeight;
        AppWindow.SetPresenter(_presenter);

        _scaleFactor = GetScaleFactor();
        AppWindow.MoveAndResize(
            new RectInt32(
                (int)(_settingsService.Current.MainWindowState.Left * _scaleFactor),
                (int)(_settingsService.Current.MainWindowState.Top * _scaleFactor),
                (int)(_settingsService.Current.MainWindowState.Width * _scaleFactor),
                (int)(_settingsService.Current.MainWindowState.Height * _scaleFactor)
            )
        );

        if (_settingsService.Current.MainWindowState.IsMaximized)
            _presenter.Maximize();

        _settingsService.SettingChanged += Settings_Changed;
        Content.KeyDown += OnKeyDown;
        Closed += MainWindow_Closed;

        _messenger.RegisterAll(this);
    }

    public void Receive(SetFullscreenMessage message) => SetFullscreenState(message.IsFullscreen);

    public void SetMicaBackdrop() => SystemBackdrop = new MicaBackdrop();

    public void NavigateToShell() =>
        RootFrame.Navigate(typeof(ShellPage), null, new DrillInNavigationTransitionInfo());

    public void NavigateToError(Exception ex) =>
        RootFrame.Navigate(
            typeof(InitializationErrorPage),
            ex,
            new DrillInNavigationTransitionInfo()
        );

    public void AttachBrowser(object browser)
    {
        if (browser is WebView2 wv)
            BrowserContainer.Children.Add(wv);
    }

    public void DetachBrowser(object browser)
    {
        if (browser is WebView2 wv)
            BrowserContainer.Children.Remove(wv);
    }

    public void ShowBrowser()
    {
        _currentStoryboard?.Stop();

        if (BrowserContainer.Children.FirstOrDefault() is not WebView2)
        {
            Log.Warning("ShowBrowser called but WebView2 not found");
            return;
        }

        SizeChanged += OnWindowSizeChangedWhileBrowserVisible;
        CancelBrowserInteractionBT.Visibility = Visibility.Visible;

        BrowserContainer.Margin = new Thickness(0);
        BrowserContainer.Width = AppWindow.Size.Width;
        BrowserContainer.Height = AppWindow.Size.Height;
        BrowserContainer.IsHitTestVisible = true;

        BrowserContainer.RenderTransform = new TranslateTransform { Y = 100 };
        BrowserContainer.Opacity = 0;

        _currentStoryboard = new Storyboard();
        var slideAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(slideAnimation, BrowserContainer.RenderTransform);
        Storyboard.SetTargetProperty(slideAnimation, "Y");
        _currentStoryboard.Children.Add(slideAnimation);

        var fadeAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fadeAnimation, BrowserContainer);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        _currentStoryboard.Children.Add(fadeAnimation);

        _currentStoryboard.Completed += (s, e) =>
        {
            BrowserContainer.Opacity = 1;
            BrowserContainer.RenderTransform = new TranslateTransform { Y = 0 };
        };

        _currentStoryboard.Begin();
    }

    public void HideBrowser()
    {
        CancelBrowserInteractionBT.Visibility = Visibility.Collapsed;

        _currentStoryboard?.Stop();
        _currentStoryboard = new Storyboard();

        var fadeAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
        };
        Storyboard.SetTarget(fadeAnimation, BrowserContainer);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        _currentStoryboard.Children.Add(fadeAnimation);

        _currentStoryboard.Completed += (s, e) =>
        {
            SizeChanged -= OnWindowSizeChangedWhileBrowserVisible;

            BrowserContainer.Margin = new Thickness(-125, -125, 0, 0);
            BrowserContainer.Width = 100;
            BrowserContainer.Height = 100;
            BrowserContainer.Opacity = 1;
            BrowserContainer.IsHitTestVisible = false;

            BrowserContainer.RenderTransform = null;
        };

        _currentStoryboard.Begin();
    }

    private void SetFullscreenState(bool isFullscreen)
    {
        if (isFullscreen)
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        else
            AppWindow.SetPresenter(_presenter);
    }

    private void SetTheme(ThemeMode themeMode)
    {
        var newTheme = themeMode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        RootElement.RequestedTheme = newTheme;

        if (newTheme == ElementTheme.Default)
            newTheme =
                Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? ElementTheme.Dark
                    : ElementTheme.Light;

        Color buttonHoverBackgroundColor =
            newTheme == ElementTheme.Dark ? Color.FromArgb(255, 61, 61, 61) : Colors.LightGray;

        Color foregroundColor = newTheme == ElementTheme.Dark ? Colors.White : Colors.Black;

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonHoverBackgroundColor = buttonHoverBackgroundColor;
        titleBar.ForegroundColor = foregroundColor;
        titleBar.ButtonForegroundColor = foregroundColor;
        titleBar.ButtonHoverForegroundColor = foregroundColor;
    }

    private void Settings_Changed(object? sender, SettingsChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.Theme))
            SetTheme(e.NewValue is ThemeMode newTheme ? newTheme : ThemeMode.System);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.F11)
        {
            _messenger.Send(new FullscreenShortcutMessage());
            e.Handled = true;
        }
    }

    private async void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        args.Handled = true;

        var isMinimized = _presenter.State == OverlappedPresenterState.Minimized;
        if (!isMinimized)
        {
            var isMaximized = _presenter.State == OverlappedPresenterState.Maximized;
            var lastWindowState = new WindowState
            {
                IsMaximized = isMaximized,
                Width = isMaximized
                    ? _settingsService.Current.MainWindowState.Width
                    : AppWindow.Size.Width / _scaleFactor,
                Height = isMaximized
                    ? _settingsService.Current.MainWindowState.Height
                    : AppWindow.Size.Height / _scaleFactor,
                Left = isMaximized
                    ? _settingsService.Current.MainWindowState.Left
                    : AppWindow.Position.X / _scaleFactor,
                Top = isMaximized
                    ? _settingsService.Current.MainWindowState.Top
                    : AppWindow.Position.Y / _scaleFactor,
            };
            _settingsService.Set(s => s.MainWindowState, lastWindowState);
        }

        await _settingsService.SaveAsync();
        await App.GetService<IBrowserService>().DisposeAsync();

        _settingsService.SettingChanged -= Settings_Changed;
        Content.KeyDown -= OnKeyDown;
        Closed -= MainWindow_Closed;

#if DEBUG || DEBUG_UNPACKAGED
        Log.Debug("Yukari Debug closing — session ended");
#else
        Log.Information("Yukari closing — session ended");
#endif
        await Log.CloseAndFlushAsync();
        Close();
    }

    private void CancelBrowserInteractionBT_Click(object sender, RoutedEventArgs e) =>
        UserCancelledInteraction?.Invoke();

    private void OnWindowSizeChangedWhileBrowserVisible(object sender, WindowSizeChangedEventArgs e)
    {
        BrowserContainer.Width = AppWindow.Size.Width;
        BrowserContainer.Height = AppWindow.Size.Height;
    }

    [DllImport("user32.dll")]
    private static extern int GetDpiForWindow(IntPtr hwnd);

    // DPI scaling factor is used to convert logical coordinates to physical pixels
    // for MoveAndResize, which operates in physical pixels on WinUI3.
    private double GetScaleFactor()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        return GetDpiForWindow(hwnd) / 96.0;
    }
}
