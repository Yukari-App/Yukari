using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.DTO;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Dialogs;

public partial class LocalComicDialogViewModel : ObservableObject
{
    private readonly IComicService _comicService;
    private readonly IDialogService _dialogService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;

    private ComicModel? _oldComic;

    public LocalChaptersFormat[] AvailableChaptersFormat { get; } =
        Enum.GetValues<LocalChaptersFormat>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    public partial ContentKey? Key { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    [NotifyCanExecuteChangedFor(nameof(SaveLocalComicCommand))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int Year { get; set; } = DateTime.Now.Year;

    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CoverPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrimaryButtonEnabled))]
    [NotifyCanExecuteChangedFor(nameof(SaveLocalComicCommand))]
    public partial string ComicFolderPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial LocalChaptersFormat SelectedChaptersFormat { get; set; } =
        LocalChaptersFormat.FolderWithImages;

    [ObservableProperty]
    public partial bool IsError { get; set; } = false;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public bool IsPrimaryButtonEnabled =>
        !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(ComicFolderPath);
    public string PrimaryButtonText =>
        _localizationService.GetString(Key == null ? "AddLocalComic" : "EditLocalComic");

    public LocalComicDialogViewModel(
        IComicService comicService,
        IDialogService dialogService,
        INotificationService notificationService,
        ILocalizationService localizationService
    )
    {
        _comicService = comicService;
        _dialogService = dialogService;
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public async Task InitializeAsync(ContentKey? comicKey)
    {
        Key = comicKey;
        if (Key == null)
            return;

        var result = await _comicService.GetComicDetailsAsync(Key);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error!;
            IsError = true;
            return;
        }

        _oldComic = result.Value!.Comic;

        if (_oldComic.ComicUrl == null || string.IsNullOrWhiteSpace(_oldComic.ComicUrl))
        {
            ErrorMessage = _localizationService.GetString("ErrorInvalidComicUrl");
            IsError = true;
            return;
        }

        Title = _oldComic.Title;
        Author = _oldComic.Author ?? "";
        Year = _oldComic.Year ?? 1;
        Tags = string.Join(",", _oldComic.Tags);
        Description = _oldComic.Description ?? "";
        CoverPath = _oldComic.CoverImageUrl ?? "";

        var (comicPath, format) = LocalComicConstants.DecodeChaptersPath(_oldComic.ComicUrl);
        ComicFolderPath = comicPath;
        SelectedChaptersFormat = format;
    }

    [RelayCommand]
    private async Task OpenCoverPickerAsync()
    {
        var coverPath = await _dialogService.OpenFilePickerAsync(
            LocalComicConstants.CoverExtensions
        );
        if (coverPath != null)
            CoverPath = coverPath;
    }

    [RelayCommand]
    private async Task OpenComicPathPickerAsync()
    {
        var comicPath = await _dialogService.OpenFolderPickerAsync();
        if (comicPath != null)
            ComicFolderPath = comicPath;
    }

    private bool CanSaveLocalComic => IsPrimaryButtonEnabled;

    [RelayCommand(CanExecute = nameof(CanSaveLocalComic))]
    private async Task SaveLocalComicAsync()
    {
        var tags = string.IsNullOrWhiteSpace(Tags)
            ? Array.Empty<string>()
            : Tags.Split(
                ",",
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        var info = new LocalComicInfo(
            Title,
            NullIfWhiteSpace(Author),
            NullIfWhiteSpace(Description),
            tags,
            Year,
            NullIfWhiteSpace(CoverPath),
            ComicFolderPath,
            SelectedChaptersFormat
        );

        var comicResult = await _comicService.UpsertLocalComicAsync(info, Key?.Id);
        if (!comicResult.IsSuccess)
        {
            _notificationService.ShowError(comicResult.Error!, comicResult.ErrorTitle!);
            return;
        }

        var comicKey = comicResult.Value!;
        if (Key == null)
        {
            var chaptersResult = await _comicService.UpsertLocalChaptersAsync(
                comicKey,
                ComicFolderPath,
                SelectedChaptersFormat
            );
            if (!chaptersResult.IsSuccess)
            {
                _notificationService.ShowError(chaptersResult.Error!, chaptersResult.ErrorTitle!);
                return;
            }
        }
        else if (_oldComic?.ComicUrl != null)
        {
            var (chaptersPath, format) = LocalComicConstants.DecodeChaptersPath(_oldComic.ComicUrl);
            if (chaptersPath != ComicFolderPath || format != SelectedChaptersFormat)
            {
                var chaptersResult = await _comicService.UpsertLocalChaptersAsync(
                    Key,
                    ComicFolderPath,
                    SelectedChaptersFormat
                );
                if (!chaptersResult.IsSuccess)
                {
                    _notificationService.ShowError(
                        chaptersResult.Error!,
                        chaptersResult.ErrorTitle!
                    );
                    return;
                }
            }
        }

        _notificationService.ShowSuccess(
            _localizationService.GetString("SuccessAddingUpdatingLocalComic")
        );
    }

    private static string? NullIfWhiteSpace(string text) =>
        !string.IsNullOrWhiteSpace(text) ? text : null;
}
