using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Yukari.Helpers;
using Yukari.Services.Storage;

namespace Yukari.Services.UI;

internal class ImageCacheService : IImageCacheService
{
    private const int MaxCacheEntries = 64;

    private readonly IDownloadService _downloadService;

    private readonly ConcurrentDictionary<string, ImageSource> _cache = new();
    private readonly ConcurrentDictionary<string, byte[]> _bytesCache = new();
    private readonly ConcurrentDictionary<string, Task> _loadTasks = new();
    private readonly ConcurrentQueue<string> _insertionQueue = new();

    public ImageCacheService(IDownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    public ImageSource GetImageSource(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null!;

        if (_cache.TryGetValue(url, out var cached))
            return cached;

        ImageSource imageSource = IsSvg(url) ? new SvgImageSource() : new BitmapImage();
        AddToCache(url, imageSource);

        _loadTasks.GetOrAdd(url, u => LoadAsync(u, imageSource));

        return imageSource;
    }

    public bool TryGetCached(string? url, out ImageSource? source)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            source = null;
            return false;
        }
        return _cache.TryGetValue(url, out source);
    }

    public async Task<(int Width, int Height)?> TryGetDimensionsAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var imageSource = GetImageSource(url);

        if (_loadTasks.TryGetValue(url, out var loadTask))
        {
            try
            {
                await loadTask;
            }
            catch
            {
                return null;
            }
        }

        return imageSource is BitmapImage { PixelWidth: > 0 } bmp
            ? (bmp.PixelWidth, bmp.PixelHeight)
            : null;
    }

    public async Task<string?> SaveCachedImageAsync(string? url, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(filePath))
            return null;

        byte[]? bytes = _bytesCache.TryGetValue(url, out var cachedBytes)
            ? cachedBytes
            : await GetImageBytesAsync(url);

        if (bytes == null)
            return null;

        await File.WriteAllBytesAsync(filePath, bytes);
        return filePath;
    }

    private void AddToCache(string key, ImageSource imageSource)
    {
        _cache[key] = imageSource;
        _insertionQueue.Enqueue(key);

        while (
            _insertionQueue.Count > MaxCacheEntries && _insertionQueue.TryDequeue(out var oldest)
        )
        {
            _cache.TryRemove(oldest, out _);
            _bytesCache.TryRemove(oldest, out _);
            _loadTasks.TryRemove(oldest, out _);
        }
    }

    private async Task LoadAsync(string url, ImageSource imageSource)
    {
        byte[]? bytes = await GetImageBytesAsync(url);
        await ApplyBytesToImageSourceAsync(url, imageSource, bytes);
    }

    public async Task<byte[]?> GetImageBytesAsync(string url) =>
        IsZipEntry(url) ? ReadZipEntryBytes(url)
        : IsLocalPath(url) ? await ReadLocalFileBytesAsync(url)
        : await _downloadService.GetImageBytesAsync(url);

    private static async Task<byte[]?> ReadLocalFileBytesAsync(string path)
    {
        try
        {
            return await File.ReadAllBytesAsync(path);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? ReadZipEntryBytes(string url)
    {
        try
        {
            PageUriHelper.TryDecodeZipEntry(url, out var zipPath, out var entryName);
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(entryName);
            if (entry == null)
                return null;

            using var entryStream = entry.Open();
            using var memStream = new MemoryStream();
            entryStream.CopyTo(memStream);
            return memStream.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private async Task ApplyBytesToImageSourceAsync(
        string url,
        ImageSource imageSource,
        byte[]? bytes
    )
    {
        if (bytes == null)
        {
            // The purpose of assigning a value that refers to a file that does not exist is to trigger the ImageSource error event.
            var errorUri = new Uri("ms-appx:///Assets/___nonexistent___");
            if (imageSource is BitmapImage bitmap)
                bitmap.UriSource = errorUri;
            else if (imageSource is SvgImageSource svg)
                svg.UriSource = errorUri;

            _cache.TryRemove(url, out _);
            _loadTasks.TryRemove(url, out _);
            return;
        }

        _bytesCache[url] = bytes;

        using var stream = new MemoryStream(bytes);
        var randomAccessStream = stream.AsRandomAccessStream();

        switch (imageSource)
        {
            case BitmapImage bitmap:
                await bitmap.SetSourceAsync(randomAccessStream);
                break;
            case SvgImageSource svg:
                await svg.SetSourceAsync(randomAccessStream);
                break;
            default:
                _cache.TryRemove(url, out _);
                _bytesCache.TryRemove(url, out _);
                _loadTasks.TryRemove(url, out _);
                break;
        }
    }

    private static bool IsSvg(string url) =>
        url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
        || url.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);

    private static bool IsZipEntry(string url) => url.StartsWith(PageUriHelper.ZipScheme);

    private static bool IsLocalPath(string path) =>
        Path.IsPathRooted(path) && !path.StartsWith("http", StringComparison.OrdinalIgnoreCase);
}
