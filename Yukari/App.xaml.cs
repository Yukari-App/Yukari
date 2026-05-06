using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
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

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(AppDataHelper.GetAppDataPath(), "Logs", "yukari-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("Yukari starting — version {Version}", AppInfoHelper.Version);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(dispose: true));

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
                GetService<ILogger<DatabaseMigrator>>(),
                $"Data Source={Path.Combine(AppDataHelper.GetDataPath(), "yukari.db")}"
            );
            await migrator.MigrateAsync();
            Log.Information("Database ready");

            var dbService = GetService<IDataService>();
            var comicService = GetService<IComicService>();

            // Removals must be processed before updates: a source could be removed and
            // re-added with the same name in the same startup cycle.
            await ProcessPendingComicSourcesRemovalsAsync(dbService, comicService);
            await ProcessPendingComicSourcesUpdatesAsync(dbService, comicService);

            await InitializeAppAsync();

            MainWindow.NavigateToShell();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize Yukari — navigating to InitializationErrorPage");
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
        Log.Information("Processed {Count} pending source removals", pending.Count);

        foreach (var source in pending)
        {
            var result = await comicService.RemoveComicSourceAsync(source.Name);
            if (!result.IsSuccess)
            {
                Log.Warning(
                    "Failed to remove pending source '{SourceName}' — will retry on next startup. Error: {Error}",
                    source.Name,
                    result.Error
                );
                // TO-DO: Notify the user that the source could not be removed
            }
        }
    }

    private async Task ProcessPendingComicSourcesUpdatesAsync(
        IDataService dbService,
        IComicService comicService
    )
    {
        var pending = await dbService.GetComicSourcesPendingUpdateAsync();
        Log.Information("Processed {Count} pending source updates", pending.Count);

        foreach (var source in pending)
        {
            var result = await comicService.UpsertComicSourceAsync(source.PendingUpdatePath!);
            if (!result.IsSuccess)
            {
                Log.Warning(
                    "Failed to update comic source {SourceName} at startup. It will not be updated on next startup. Error: {Error}",
                    source.Name,
                    result.Error
                );
                // TO-DO: Invalid or corrupted plugin, notify the user and remove the pending update so it doesn't keep trying on every startup
            }
            await dbService.UpdateComicSourcePendingUpdateAsync(source.Name, null);
        }
    }
}
