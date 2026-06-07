using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Enums;
using Yukari.Models;
using Yukari.Services.Storage;

namespace Yukari.ViewModels.Pages;

public partial class DownloadsPageViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;

    public ReadOnlyObservableCollection<DownloadItem> Downloads { get; set; }

    public bool NoDownloads => Downloads.Count == 0;

    public DownloadsPageViewModel(IDownloadService downloadService)
    {
        _downloadService = downloadService;

        Downloads = _downloadService.GetAllDownloads();
        _downloadService.DownloadsCollectionChanged += OnDownloadsCollectionChanged;
    }

    public void OnNavigatedFrom() =>
        _downloadService.DownloadsCollectionChanged -= OnDownloadsCollectionChanged;

    [RelayCommand]
    private void ClearFinishedDownloads() => _downloadService.ClearFinishedDownloads();

    [RelayCommand]
    private void CancelAllUnfinishedDownloads()
    {
        foreach (var item in Downloads)
        {
            if (item.Status is DownloadStatus.Downloading or DownloadStatus.Queued)
                item.Cancel();
        }
    }

    [RelayCommand]
    private void OnCancelOrRetryItemClick(DownloadItem item)
    {
        switch (item.Status)
        {
            case DownloadStatus.Downloading:
            case DownloadStatus.Queued:
                CancelDownload(item);
                break;
            case DownloadStatus.Failed:
            case DownloadStatus.Cancelled:
                RetryDownload(item);
                break;
        }
    }

    [RelayCommand]
    private async Task OnDeleteItemClickAsync(DownloadItem item) =>
        await _downloadService.DeleteChapterDownloadAsync(item.ComicKey, item.ChapterKey);

    private void CancelDownload(DownloadItem item)
    {
        if (item.Status is DownloadStatus.Downloading or DownloadStatus.Queued)
            item.Cancel();
    }

    private void RetryDownload(DownloadItem item)
    {
        if (item.Status is DownloadStatus.Failed or DownloadStatus.Cancelled)
            _downloadService.EnqueueChapterDownload(
                item.ComicKey,
                item.ChapterKey,
                item.ComicTitle,
                item.ChapterTitle,
                item.PageProvider
            );
    }

    private void OnDownloadsCollectionChanged(object? sender, EventArgs e) =>
        OnPropertyChanged(nameof(NoDownloads));
}
