using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;
using Windows.Graphics;
using Yukari.Services;
using Yukari.Services.Comics;
using Yukari.Services.Platform;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;
using Yukari.Views.Pages;

namespace Yukari
{
    public partial class App : Application
    {
        private static Window? MainWindow;
        private readonly IServiceProvider _services;

        public App()
        {
            InitializeComponent();
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; // Change the default language of the application

            var services = new ServiceCollection();

            services.AddTransient<NavigationPageViewModel>();
            services.AddTransient<ReaderPageViewModel>();
            services.AddTransient<FavoritesPageViewModel>();
            services.AddTransient<DiscoverPageViewModel>();
            services.AddTransient<DownloadsPageViewModel>();
            services.AddTransient<SettingsPageViewModel>();
            services.AddTransient<ComicPageViewModel>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IDataService, DataService>();
            services.AddSingleton<ISourceService, SourceService>();
            services.AddSingleton<IComicService, ComicService>();

            _services = services.BuildServiceProvider();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var rootFrame = new Frame { Content = new SplashPage() };

            MainWindow = new Window
            {
                ExtendsContentIntoTitleBar = true,
                Title = "Yukari",
                Content = rootFrame
            };

            ConfigureWindowSizeAndIcons(MainWindow);

            await Task.Delay(75);
            MainWindow.Activate();

            await InitializeAppAsync();

            MainWindow.SystemBackdrop = new MicaBackdrop();
            rootFrame.Navigate(typeof(ShellPage), null, new DrillInNavigationTransitionInfo());
        }

        public static T GetService<T>() where T : class => ((App)Current)._services.GetRequiredService<T>();

        private void ConfigureWindowSizeAndIcons(Window window)
        {
            window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            window.AppWindow.SetIcon("Assets/AppIcon.ico");

            var win32Service = new Win32WindowService(window);
            win32Service.SetWindowMinMaxSize(new Win32WindowService.POINT() { x = 656, y = 500 });

            var scaleFactor = win32Service.GetSystemDPI() / 96.0;
            window.AppWindow.Resize(new SizeInt32((int)(1200 * scaleFactor), (int)(700 * scaleFactor)));
        }

        private async Task InitializeAppAsync()
        {
            await Task.Delay(400);
        }
    }
}
