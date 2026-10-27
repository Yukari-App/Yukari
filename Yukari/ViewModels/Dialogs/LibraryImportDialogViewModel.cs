using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Yukari.Enums;
using Yukari.Messages;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Dialogs;

public partial class LibraryImportDialogViewModel : ObservableObject
{
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly IMessenger _messenger;
    private readonly ILocalizationService _localizationService;

    private CancellationTokenSource? _importCTS;

    public LibraryImportMode[] AvailableImportModes { get; } = Enum.GetValues<LibraryImportMode>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImportAvailable))]
    public partial string ArchivePath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMergeMode), nameof(IsReplaceMode))]
    public partial LibraryImportMode SelectedImportMode { get; set; } = LibraryImportMode.Merge;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsFormVisible),
        nameof(CloseButtonText),
        nameof(IsImportAvailable)
    )]
    public partial bool IsImporting { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial bool IsImported { get; set; } = false;

    [ObservableProperty]
    public partial bool IsPendingRestart { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial bool IsError { get; set; } = false;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int ImportProgress { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(IsProgressIndeterminate))]
    public partial int ImportTotal { get; set; } = 0;

    public bool IsMergeMode => SelectedImportMode == LibraryImportMode.Merge;

    public bool IsReplaceMode => SelectedImportMode == LibraryImportMode.Replace;

    public bool IsFormVisible => !IsImporting && !IsImported && !IsError;

    public bool IsImportAvailable => !string.IsNullOrWhiteSpace(ArchivePath) && IsFormVisible;

    public bool IsProgressIndeterminate => ImportTotal == 0;

    public string ProgressText =>
        _localizationService.GetFormattedString(
            "LibraryImportProgressText",
            ImportProgress,
            ImportTotal
        );

    public string CloseButtonText =>
        _localizationService.GetString(IsImporting ? "Cancel" : "Close");

    public LibraryImportDialogViewModel(
        IExportService exportService,
        IDialogService dialogService,
        IMessenger messenger,
        ILocalizationService localizationService
    )
    {
        _exportService = exportService;
        _dialogService = dialogService;
        _messenger = messenger;
        _localizationService = localizationService;
    }

    [RelayCommand]
    private async Task OpenArchiveFilePickerAsync()
    {
        var filePath = await _dialogService.OpenFilePickerAsync([".zip"]);
        if (filePath != null)
            ArchivePath = filePath;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(ArchivePath))
            return;

        _importCTS = new();
        IsImporting = true;

        var progress = new Progress<(int, int)>(UpdateProgress);
        var result = await _exportService.ImportLibraryAsync(
            ArchivePath,
            SelectedImportMode,
            progress,
            _importCTS.Token
        );

        if (result.IsSuccess)
        {
            IsImported = true;
            _messenger.Send(new ComicSourcesUpdatedMessage());
        }
        else if (result.Kind == ResultKind.PendingRestart)
        {
            IsImported = true;
            IsPendingRestart = true;
            _messenger.Send(new ComicSourcesUpdatedMessage());
        }
        else if (result.IsCancelled) { }
        else
        {
            IsError = true;
            ErrorMessage = result.Error!;
        }

        IsImporting = false;
    }

    [RelayCommand]
    private void CancelImport()
    {
        if (IsImporting)
            _importCTS?.Cancel();
    }

    private void UpdateProgress((int current, int total) progress)
    {
        ImportProgress = progress.current;
        ImportTotal = progress.total;
    }
}
