using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Yukari.Helpers;
using Yukari.Services;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Pages;

namespace Yukari;

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

        try
        {
            var migrator = new DatabaseMigrator(
                $"Data Source={Path.Combine(AppDataHelper.GetDataPath(), "yukari.db")}"
            );
            await migrator.MigrateAsync();

            var dbService = GetService<IDataService>();
            var comicService = GetService<IComicService>();
            await ProcessPendingComicSourcesRemovalsAsync(dbService, comicService);
            await ProcessPendingComicSourcesUpdatesAsync(dbService, comicService);

            await InitializeAppAsync();

            MainWindow.NavigateToShell();
        }
        catch (Exception ex)
        {
            MainWindow.NavigateToError(ex);
        }
        finally
        {
            MainWindow.SetMicaBackdrop();
        }
    }

    public static T GetService<T>()
        where T : class => ((App)Current)._services.GetRequiredService<T>();

    private async Task InitializeAppAsync()
    {
        await Task.Delay(400);
    }

    private async Task ProcessPendingComicSourcesRemovalsAsync(
        IDataService dbService,
        IComicService comicService
    )
    {
        var pending = await dbService.GetComicSourcesPendingRemovalAsync();
        foreach (var source in pending)
        {
            var result = await comicService.RemoveComicSourceAsync(source.Name);
            if (!result.IsSuccess)
            {
                // TO-DO: Log the error and notify the user that the source could not be removed
            }
        }
    }

    private async Task ProcessPendingComicSourcesUpdatesAsync(
        IDataService dbService,
        IComicService comicService
    )
    {
        var pending = await dbService.GetComicSourcesPendingUpdateAsync();
        foreach (var source in pending)
        {
            var result = await comicService.UpsertComicSourceAsync(source.PendingUpdatePath!);
            if (!result.IsSuccess)
            {
                // TO-DO: Invalid or corrupted plugin, notify it and log the error
            }
            await dbService.UpdateComicSourcePendingUpdateAsync(source.Name, null);
        }
    }
}
