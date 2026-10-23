using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Yukari.Enums;
using Yukari.Helpers;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.Services.Sources;

internal class LocalSourceService : ILocalSourceService
{
    private readonly ILogger<LocalSourceService> _logger;

    public LocalSourceService(ILogger<LocalSourceService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<LocalChapterScanResult>> ScanChaptersAsync(
        string comicDirectory,
        LocalChapterFormat format,
        CancellationToken ct = default
    )
    {
        if (!Directory.Exists(comicDirectory))
        {
            _logger.LogWarning("Comic directory not found: {Directory}", comicDirectory);
            return Array.Empty<LocalChapterScanResult>();
        }

        return await Task.Run(
            () =>
                format switch
                {
                    LocalChapterFormat.FolderWithImages => ScanFolderChapters(comicDirectory),
                    LocalChapterFormat.Cbz => ScanArchiveChapters(
                        comicDirectory,
                        "*.cbz",
                        ReadCbzArchive
                    ),
                    LocalChapterFormat.Epub => ScanArchiveChapters(
                        comicDirectory,
                        "*.epub",
                        ReadEpubArchive
                    ),
                    _ => Array.Empty<LocalChapterScanResult>(),
                },
            ct
        );
    }

    private static LocalChapterScanResult[] ScanFolderChapters(string comicDirectory)
    {
        var chapterDirs = Directory
            .GetDirectories(comicDirectory)
            .OrderBy(d => d, Comparer<string>.Create(NaturalCompare));

        return chapterDirs
            .Select(dir =>
            {
                var dirName = Path.GetFileName(dir);
                var pages = GetImageFiles(dir)
                    .Select(
                        (imagePath, index) =>
                            new ChapterPageModel { Number = index + 1, ImageUrl = imagePath }
                    )
                    .ToArray();

                var chapter = new ChapterModel
                {
                    Id = dirName,
                    Source = LocalComicHelper.SourceName,
                    Title = dirName,
                    Language = LocalComicHelper.SourceName,
                    Pages = pages.Length,
                    LastUpdate = DateOnly.FromDateTime(Directory.GetLastWriteTime(dir)),
                };

                return new LocalChapterScanResult(chapter, pages);
            })
            .ToArray();
    }

    private LocalChapterScanResult[] ScanArchiveChapters(
        string comicDirectory,
        string searchPattern,
        Func<ZipArchive, (string? Title, IReadOnlyList<string> PageEntries)> archiveReader
    )
    {
        var archiveFiles = Directory
            .GetFiles(comicDirectory, searchPattern)
            .OrderBy(f => f, Comparer<string>.Create(NaturalCompare));

        var results = archiveFiles
            .Select(file =>
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var (extractedTitle, pages) = ReadArchive(file, archiveReader);

                var chapter = new ChapterModel
                {
                    Id = fileName,
                    Source = LocalComicHelper.SourceName,
                    Title = !string.IsNullOrWhiteSpace(extractedTitle) ? extractedTitle : fileName,
                    Language = LocalComicHelper.SourceName,
                    Pages = pages.Length,
                    LastUpdate = DateOnly.FromDateTime(File.GetLastWriteTime(file)),
                };

                return new LocalChapterScanResult(chapter, pages);
            })
            .ToArray();

        foreach (
            var duplicateGroup in results
                .GroupBy(r => r.Chapter.Title?.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
        )
        {
            foreach (var item in duplicateGroup)
                item.Chapter.Title = item.Chapter.Id;
        }

        return results;
    }

    private (string? Title, ChapterPageModel[] Pages) ReadArchive(
        string archivePath,
        Func<ZipArchive, (string? Title, IReadOnlyList<string> PageEntries)> archiveReader
    )
    {
        if (!File.Exists(archivePath))
            return (null, Array.Empty<ChapterPageModel>());

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var (title, pageEntries) = archiveReader(archive);

            var pages = pageEntries
                .Select(
                    (entryFullName, index) =>
                        new ChapterPageModel
                        {
                            Number = index + 1,
                            ImageUrl = ImageUriHelper.EncodeZipEntry(archivePath, entryFullName),
                        }
                )
                .ToArray();

            return (title, pages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read archive: {ArchivePath}", archivePath);
            return (null, Array.Empty<ChapterPageModel>());
        }
    }

    private static (string? Title, IReadOnlyList<string> PageEntries) ReadCbzArchive(
        ZipArchive archive
    ) => (null, GetNaturalSortedImageEntries(archive));

    private static (string? Title, IReadOnlyList<string> PageEntries) ReadEpubArchive(
        ZipArchive archive
    )
    {
        var (title, pages) = EpubHelper.ExtractContent(archive);
        if (pages.Count == 0)
            pages = GetNaturalSortedImageEntries(archive);

        return (title, pages);
    }

    private static List<string> GetNaturalSortedImageEntries(ZipArchive archive) =>
        archive
            .Entries.Where(e => LocalComicHelper.IsPageImageFile(e.Name))
            .OrderBy(e => e.FullName, Comparer<string>.Create(NaturalCompare))
            .Select(e => e.FullName)
            .ToList();

    private static List<string> GetImageFiles(string directory) =>
        Directory
            .GetFiles(directory)
            .Where(LocalComicHelper.IsPageImageFile)
            .OrderBy(f => f, Comparer<string>.Create(NaturalCompare))
            .ToList();

    private static int NaturalCompare(string? a, string? b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return -1;
        if (b == null)
            return 1;

        int i = 0,
            j = 0;
        while (i < a.Length && j < b.Length)
        {
            int startA = i,
                startB = j;
            while (i < a.Length && !char.IsDigit(a[i]))
                i++;
            while (j < b.Length && !char.IsDigit(b[j]))
                j++;

            int textCompare = string.Compare(
                a[startA..i],
                b[startB..j],
                StringComparison.OrdinalIgnoreCase
            );
            if (textCompare != 0)
                return textCompare;

            startA = i;
            startB = j;
            while (i < a.Length && char.IsDigit(a[i]))
                i++;
            while (j < b.Length && char.IsDigit(b[j]))
                j++;

            string numA = a[startA..i],
                numB = b[startB..j];
            if (long.TryParse(numA, out long valA) && long.TryParse(numB, out long valB))
            {
                if (valA != valB)
                    return valA.CompareTo(valB);
            }
            else
            {
                int numCompare = string.Compare(numA, numB, StringComparison.Ordinal);
                if (numCompare != 0)
                    return numCompare;
            }
        }
        return a.Length.CompareTo(b.Length);
    }
}
