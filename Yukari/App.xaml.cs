using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Serilog;
using Windows.ApplicationModel.Activation;
using Yukari.Helpers;
using Yukari.Services;
using Yukari.Services.Comics;
using Yukari.Services.Settings;
using Yukari.Services.Sources;
using Yukari.Services.Storage;
using Yukari.Services.UI;
using Yukari.ViewModels.Dialogs;
using Yukari.ViewModels.Pages;

namespace Yukari;

public partial class App : Application
{
    private const string AppInstanceKey = "Yukari.MainApp";
    private static MainWindow? _mainWindow;

    private readonly ServiceProvider _services;

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
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("Yukari starting — version {Version}", AppInfoHelper.Version);

        _services = ConfigureServices();
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var currentInstance = AppInstance.FindOrRegisterForKey(AppInstanceKey);
        if (!currentInstance.IsCurrent)
        {
            await currentInstance.RedirectActivationToAsync(
                AppInstance.GetCurrent().GetActivatedEventArgs()
            );

            Environment.Exit(0);
            return;
        }
        currentInstance.Activated += OnAppInstanceActivated;

        var settings = GetService<ISettingsService>();
        await settings.LoadAsync();

        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        try
        {
            await InitializeDatabaseAsync();
            await ProcessPendingComicSourcesAsync();

            await Task.Delay(400); // Small delay to ensure the main window is fully ready before navigating

            _mainWindow.NavigateToShell();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize Yukari — navigating to InitializationErrorPage");
            _mainWindow.NavigateToError(ex);
        }
        finally
        {
            _mainWindow.SetMicaBackdrop();
        }
    }

    public static T GetService<T>()
        where T : class => ((App)Current)._services.GetRequiredService<T>();

    // --- Configuration ---

    private static ServiceProvider ConfigureServices()
    {
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
        services.AddTransient<CollectionsManagerDialogViewModel>();
        services.AddTransient<ComicCollectionsDialogViewModel>();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDataService, DataService>();
        services.AddSingleton<IDownloadService, DownloadService>();
        services.AddSingleton<ISourceService, SourceService>();
        services.AddSingleton<ILocalSourceService, LocalSourceService>();
        services.AddSingleton<IComicService, ComicService>();

        return services.BuildServiceProvider();
    }

    // --- Initialization Tasks ---

    private static async Task InitializeDatabaseAsync()
    {
        var migrator = new DatabaseMigrator(
            GetService<ILogger<DatabaseMigrator>>(),
            $"Data Source={Path.Combine(AppDataHelper.GetDataPath(), "yukari.db")}"
        );

        await migrator.MigrateAsync();
        Log.Information("Database ready");
    }

    private static async Task ProcessPendingComicSourcesAsync()
    {
        var dbService = GetService<IDataService>();
        var comicService = GetService<IComicService>();
        var notificationService = GetService<INotificationService>();

        // Removals must be processed before updates: a source could be removed and
        // re-added with the same name in the same startup cycle.
        await ProcessPendingComicSourcesRemovalsAsync(dbService, comicService, notificationService);
        await ProcessPendingComicSourcesUpdatesAsync(dbService, comicService, notificationService);
    }

    private static async Task ProcessPendingComicSourcesRemovalsAsync(
        IDataService dbService,
        IComicService comicService,
        INotificationService notificationService
    )
    {
        var pending = await dbService.GetComicSourcesPendingRemovalAsync();
        Log.Information("Processing {Count} pending source removals", pending.Count);

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
                notificationService.ShowWarning(
                    $"The source '{source.Name}' could not be removed. Retrying on next startup",
                    "Failed to Remove Comic Source"
                );
            }
        }
    }

    private static async Task ProcessPendingComicSourcesUpdatesAsync(
        IDataService dbService,
        IComicService comicService,
        INotificationService notificationService
    )
    {
        var pending = await dbService.GetComicSourcesPendingUpdateAsync();
        Log.Information("Processing {Count} pending source updates", pending.Count);

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
                notificationService.ShowError(
                    $"The source '{source.Name}' could not be updated and will not be updated on next startup. Try updating manually.",
                    "Failed to Update Comic Source"
                );
            }
            await dbService.UpdateComicSourcePendingUpdateAsync(source.Name, null);
        }
    }

    // --- Activation Handling ---

    private void OnAppInstanceActivated(object? sender, AppActivationArguments e)
    {
        _mainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            WindowHelper.BringToFront(_mainWindow);

            if (e.Kind == ExtendedActivationKind.Protocol)
            {
                var uri = (e.Data as ProtocolActivatedEventArgs)?.Uri;
                // Handle deep links in the future if necessary
            }
        });
    }
}
