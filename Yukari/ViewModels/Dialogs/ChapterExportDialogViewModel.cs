using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models.DTO;
using Yukari.Services.Storage;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Dialogs;

public partial class ChapterExportDialogViewModel : ObservableObject
{
    private readonly IExportService _exportService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;

    private ContentKey? _comicKey;
    private ContentKey? _chapterKey;

    private string ComicTitle = string.Empty;
    private string ChapterTitle = string.Empty;
    private string DefaultExportName => SanitizeFileName($"{ComicTitle} - {ChapterTitle}");

    private CancellationTokenSource? _exportCTS;

    public ExportFormat[] AvailableExportFormats { get; } = Enum.GetValues<ExportFormat>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportAvailable))]
    public partial string ExportName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExportAvailable))]
    public partial string DestinationPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ExportFormat SelectedExportFormat { get; set; } = ExportFormat.FolderWithImages;

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
        _localizationService.GetFormattedString("ExportProgressText", ExportProgress, ExportTotal);

    public string CloseButtonText =>
        _localizationService.GetString(IsExporting ? "Cancel" : "Close");

    public ChapterExportDialogViewModel(
        IExportService exportService,
        IDialogService dialogService,
        ILocalizationService localizationService
    )
    {
        _exportService = exportService;
        _dialogService = dialogService;
        _localizationService = localizationService;
    }

    public void Initialize(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle
    )
    {
        _comicKey = comicKey;
        _chapterKey = chapterKey;
        ComicTitle = comicTitle;
        ChapterTitle = chapterTitle;

        ExportName = DefaultExportName;
    }

    [RelayCommand]
    private async Task OpenDestinationPathPickerAsync()
    {
        var comicPath = await _dialogService.OpenFolderPickerAsync();
        if (comicPath != null)
            DestinationPath = comicPath;
    }

    [RelayCommand]
    private async Task Export()
    {
        if (_comicKey == null || _chapterKey == null)
            return;

        _exportCTS = new();
        IsExporting = true;

        var exportName = string.IsNullOrWhiteSpace(ExportName) ? DefaultExportName : ExportName;
        var finalPath = Path.Combine(DestinationPath, exportName);
        finalPath += SelectedExportFormat switch
        {
            ExportFormat.Cbz => ".cbz",
            _ => string.Empty,
        };

        var progress = new Progress<(int, int)>(UpdateProgress);
        var result = await _exportService.ExportChapterAsync(
            _comicKey,
            _chapterKey,
            SelectedExportFormat,
            finalPath,
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

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(name.Where(c => !invalidChars.Contains(c)));
    }
}
