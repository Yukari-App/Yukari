using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
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

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<MainPageViewModel>();
            services.AddTransient<FavoritesPageViewModel>();
            services.AddTransient<DiscoverPageViewModel>();
            services.AddTransient<DownloadsPageViewModel>();
            services.AddTransient<SettingsPageViewModel>();
            services.AddTransient<ComicPageViewModel>();

            _services = services.BuildServiceProvider();
        }


        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // Load Main Window
            MainWindow = new Window
            {
                SystemBackdrop = new MicaBackdrop(),
                ExtendsContentIntoTitleBar = true,
                Title = "Yukari",
                Content = new MainPage()
            };

            MainWindow.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");

            var win32WindowService = new Win32WindowService(MainWindow);
            win32WindowService.SetWindowMinMaxSize(new Win32WindowService.POINT() { x = 656, y = 500 });

            var scaleFactor = win32WindowService.GetSystemDPI() / 96.0;
            MainWindow.AppWindow.Resize(new SizeInt32((int)(1200 * scaleFactor), (int)(700 * scaleFactor)));

            MainWindow.Activate();
        }

        public static T GetService<T>() where T : class => ((App)Current)._services.GetRequiredService<T>();
    }
}
