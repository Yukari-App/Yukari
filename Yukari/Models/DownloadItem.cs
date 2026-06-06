using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Yukari.Enums;
using Yukari.Models.DTO;

namespace Yukari.Models;

public partial class DownloadItem : ObservableObject
{
    public ContentKey ComicKey { get; }
    public ContentKey ChapterKey { get; }
    public string ChapterTitle { get; }

    public IReadOnlyList<ChapterPageModel> Pages { get; }

    [ObservableProperty]
    public partial DownloadStatus Status { get; set; } = DownloadStatus.Queued;

    [ObservableProperty]
    public partial double Progress { get; set; } = 0.0;

    internal CancellationTokenSource Cts { get; } = new();

    public DownloadItem(
        ContentKey comicKey,
        ContentKey chapterKey,
        string chapterTitle,
        IReadOnlyList<ChapterPageModel> pages
    )
    {
        ComicKey = comicKey;
        ChapterKey = chapterKey;
        ChapterTitle = chapterTitle;
        Pages = pages;
    }

    public void Cancel()
    {
        Cts.Cancel();
        Status = DownloadStatus.Cancelled;
    }
}
