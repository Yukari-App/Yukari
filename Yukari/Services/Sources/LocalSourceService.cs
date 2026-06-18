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

namespace Yukari.Services.Sources;

internal class LocalSourceService : ILocalSourceService
{
    private readonly ILogger<LocalSourceService> _logger;

    public LocalSourceService(ILogger<LocalSourceService> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChapterModel>> ScanChaptersAsync(
        string comicDirectory,
        LocalChaptersFormat format,
        CancellationToken ct = default
    )
    {
        if (!Directory.Exists(comicDirectory))
        {
            _logger.LogWarning("Comic directory not found: {Directory}", comicDirectory);
            return Array.Empty<ChapterModel>();
        }

        return await Task.Run(
            () =>
                format switch
                {
                    LocalChaptersFormat.FolderWithImages => ScanFolderChapters(comicDirectory),
                    LocalChaptersFormat.Cbz => ScanCbzChapters(comicDirectory),
                    _ => Array.Empty<ChapterModel>(),
                },
            ct
        );
    }

    public async Task<IReadOnlyList<ChapterPageModel>> GetPagesAsync(
        string chapterPath,
        LocalChaptersFormat format,
        CancellationToken ct = default
    ) =>
        await Task.Run(
            () =>
                format switch
                {
                    LocalChaptersFormat.FolderWithImages => GetFolderPages(chapterPath),
                    LocalChaptersFormat.Cbz => GetCbzPages(chapterPath),
                    _ => Array.Empty<ChapterPageModel>(),
                },
            ct
        );

    private ChapterModel[] ScanFolderChapters(string comicDirectory)
    {
        var chapterDirs = Directory
            .GetDirectories(comicDirectory)
            .OrderBy(d => d, Comparer<string>.Create(NaturalCompare));

        return chapterDirs
            .Select(
                (dir, index) =>
                {
                    var dirName = Path.GetFileName(dir);
                    var images = GetImageFiles(dir);

                    return new ChapterModel
                    {
                        Id = dirName,
                        Source = LocalComicConstants.SourceName,
                        Title = dirName,
                        Language = LocalComicConstants.SourceName,
                        Pages = images.Count,
                        LastUpdate = DateOnly.FromDateTime(Directory.GetLastWriteTime(dir)),
                        LocalPath = dir,
                    };
                }
            )
            .ToArray();
    }

    private ChapterPageModel[] GetFolderPages(string chapterPath)
    {
        if (!Directory.Exists(chapterPath))
            return Array.Empty<ChapterPageModel>();

        var images = GetImageFiles(chapterPath);

        return images
            .Select(
                (imagePath, index) =>
                    new ChapterPageModel { Number = index + 1, ImageUrl = imagePath }
            )
            .ToArray();
    }

    private ChapterModel[] ScanCbzChapters(string comicDirectory)
    {
        var cbzFiles = Directory
            .GetFiles(comicDirectory, "*.cbz")
            .OrderBy(f => f, Comparer<string>.Create(NaturalCompare));

        return cbzFiles
            .Select(
                (file, index) =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var pageCount = CountCbzEntries(file);

                    return new ChapterModel
                    {
                        Id = fileName,
                        Source = LocalComicConstants.SourceName,
                        Title = fileName,
                        Language = LocalComicConstants.SourceName,
                        Pages = pageCount,
                        LastUpdate = DateOnly.FromDateTime(File.GetLastWriteTime(file)),
                        LocalPath = file,
                    };
                }
            )
            .ToArray();
    }

    private ChapterPageModel[] GetCbzPages(string chapterPath)
    {
        if (!File.Exists(chapterPath))
            return Array.Empty<ChapterPageModel>();

        try
        {
            using var archive = ZipFile.OpenRead(chapterPath);
            var images = archive
                .Entries.Where(e => IsImageFile(e.Name))
                .OrderBy(e => e.FullName, Comparer<string>.Create(NaturalCompare))
                .ToList();

            return images
                .Select(
                    (entry, index) =>
                        new ChapterPageModel
                        {
                            Number = index + 1,
                            ImageUrl = $"zip:///{chapterPath}#{entry.FullName}",
                        }
                )
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read CBZ: {CbzPath}", chapterPath);
            return Array.Empty<ChapterPageModel>();
        }
    }

    private static List<string> GetImageFiles(string directory)
    {
        return Directory
            .GetFiles(directory)
            .Where(IsImageFile)
            .OrderBy(f => f, Comparer<string>.Create(NaturalCompare))
            .ToList();
    }

    private static bool IsImageFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";
    }

    private static int CountCbzEntries(string cbzPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(cbzPath);
            return archive.Entries.Count(e => IsImageFile(e.Name));
        }
        catch
        {
            return 0;
        }
    }

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
