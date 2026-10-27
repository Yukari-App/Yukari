using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Helpers;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Dialogs;

public partial class LibraryExportDialogViewModel : ObservableObject
{
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    private CancellationTokenSource? _exportCTS;

    private static string DefaultExportName => $"YukariBackup_{DateTime.Now:yyyy-MM-dd}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportAvailable))]
    public partial string ExportName { get; set; } = DefaultExportName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportAvailable))]
    public partial string DestinationPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeDownloads { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludePlugins { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(IsFormVisible),
        nameof(CloseButtonText),
        nameof(IsExportAvailable)
    )]
    public partial bool IsExporting { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial bool IsExported { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial bool IsError { get; set; } = false;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    public partial int ExportProgress { get; set; } = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText), nameof(IsProgressIndeterminate))]
    public partial int ExportTotal { get; set; } = 0;

    public bool IsFormVisible => !IsExporting && !IsExported && !IsError;

    public bool IsExportAvailable => !string.IsNullOrWhiteSpace(DestinationPath) && IsFormVisible;

    public bool IsProgressIndeterminate => ExportTotal == 0;

    public string ProgressText =>
        _localizationService.GetFormattedString(
            "LibraryExportProgressText",
            ExportProgress,
            ExportTotal
        );

    public string CloseButtonText =>
        _localizationService.GetString(IsExporting ? "Cancel" : "Close");

    public LibraryExportDialogViewModel(
        IExportService exportService,
        IDialogService dialogService,
        ILocalizationService localizationService
    )
    {
        _exportService = exportService;
        _dialogService = dialogService;
        _localizationService = localizationService;
    }

    [RelayCommand]
    private async Task OpenDestinationPathPickerAsync()
    {
        var folderPath = await _dialogService.OpenFolderPickerAsync();
        if (folderPath != null)
            DestinationPath = folderPath;
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (string.IsNullOrWhiteSpace(DestinationPath))
            return;

        _exportCTS = new();
        IsExporting = true;

        var sanitizedName = FileHelper.SanitizeFileName(ExportName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
            sanitizedName = DefaultExportName;

        if (!sanitizedName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            sanitizedName += ".zip";

        var finalPath = Path.Combine(DestinationPath, sanitizedName);

        var progress = new Progress<(int, int)>(UpdateProgress);
        var result = await _exportService.ExportLibraryAsync(
            finalPath,
            IncludeDownloads,
            IncludePlugins,
            progress,
            _exportCTS.Token
        );

        if (result.IsSuccess)
        {
            IsExported = true;
        }
        else if (result.IsCancelled) { }
        else
        {
            IsError = true;
            ErrorMessage = result.Error!;
        }

        IsExporting = false;
    }

    [RelayCommand]
    private void CancelExport()
    {
        if (IsExporting)
            _exportCTS?.Cancel();
    }

    private void UpdateProgress((int current, int total) progress)
    {
        ExportProgress = progress.current;
        ExportTotal = progress.total;
    }
}
