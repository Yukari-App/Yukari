using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Yukari.Services;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;

namespace Yukari
{
    public partial class App : Application
    {
        private static MainWindow? MainWindow;

        private readonly IServiceProvider _services;

        public App()
        {
            InitializeComponent();
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; // Change the default language of the application

            var services = new ServiceCollection();
            services.AddTransient<ShellPageViewModel>();
            services.AddTransient<NavigationPageViewModel>();
            services.AddTransient<ReaderPageViewModel>();
            services.AddTransient<FavoritesPageViewModel>();
            services.AddTransient<DiscoverPageViewModel>();
            services.AddTransient<DownloadsPageViewModel>();
            services.AddTransient<SettingsPageViewModel>();
            services.AddTransient<ComicPageViewModel>();

            services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDataService, DataService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<ISourceService, SourceService>();
            services.AddSingleton<IComicService, ComicService>();
            _services = services.BuildServiceProvider();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settings = GetService<ISettingsService>();
            await settings.LoadAsync();

            MainWindow = new MainWindow();
            MainWindow.Activate();

            await InitializeAppAsync();

            MainWindow.SetMicaBackdrop();
            MainWindow.NavigateToShell();
        }

        public static T GetService<T>()
            where T : class => ((App)Current)._services.GetRequiredService<T>();

        private async Task InitializeAppAsync()
        {
            await Task.Delay(400);
        }
    }
}
