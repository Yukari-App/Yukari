using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Enums;
using Yukari.Models.Common;
using Yukari.Models.DTO;

namespace Yukari.Models;

public partial class DownloadItem : ObservableObject
{
    public ContentKey ComicKey { get; }
    public ContentKey ChapterKey { get; }
    public string ComicTitle { get; }
    public string ChapterTitle { get; }

    public Func<
        CancellationToken,
        Task<Result<IReadOnlyList<ChapterPageModel>>>
    > PageProvider { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel), nameof(CanRetry))]
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedProgress))]
    public partial double Progress { get; set; } = 0.0;

    public string FormattedProgress => $"{Progress * 100:00}%";

    public bool CanCancel => Status is DownloadStatus.Queued or DownloadStatus.Downloading;
    public bool CanRetry => Status is DownloadStatus.Failed or DownloadStatus.Cancelled;

    internal CancellationTokenSource Cts { get; } = new();

    public DownloadItem(
        ContentKey comicKey,
        ContentKey chapterKey,
        string comicTitle,
        string chapterTitle,
        Func<CancellationToken, Task<Result<IReadOnlyList<ChapterPageModel>>>> pageProvider
    )
    {
        ComicKey = comicKey;
        ChapterKey = chapterKey;
        ChapterTitle = chapterTitle;
        ComicTitle = comicTitle;
        PageProvider = pageProvider;
    }

    public void Cancel()
    {
        if (!CanCancel)
            return;
        Cts.Cancel();
        Status = DownloadStatus.Cancelled;
    }
}
