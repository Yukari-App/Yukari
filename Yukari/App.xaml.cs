using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Graphics;
using Yukari.Services;
using Yukari.ViewModels;
using Yukari.Views;

namespace Yukari
{
    public partial class App : Application
    {
        private static Window? MainWindow;

        public IServiceProvider Services { get; }

        public App()
        {
            InitializeComponent();
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; // Change the default language of the application

            var services = new ServiceCollection();

            // Registrar seus serviços
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddTransient<MainPageViewModel>();
            services.AddTransient<FavoritesPageViewModel>();

            Services = services.BuildServiceProvider();
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

            MainWindow.AppWindow.SetIcon("Assets/AppIcon.ico");

            var win32WindowService = new Win32WindowService(MainWindow);
            win32WindowService.SetWindowMinMaxSize(new Win32WindowService.POINT() { x = 600, y = 500 });

            var scaleFactor = win32WindowService.GetSystemDPI() / 96.0;
            MainWindow.AppWindow.Resize(new SizeInt32((int)(1200 * scaleFactor), (int)(700 * scaleFactor)));

            MainWindow.Activate();
        }

        public INavigationService NavigationService => Services.GetService<INavigationService>();
    }
}
