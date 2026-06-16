using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Yukari.Services.Storage;

namespace Yukari.Helpers.UI.Converters;

public class UrlToImageConverter : IValueConverter
{
    private const int MaxCacheEntries = 64;

    private static readonly ConcurrentDictionary<string, ImageSource> _cache = new();
    private static readonly ConcurrentQueue<string> _insertionQueue = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string imageUrl || string.IsNullOrWhiteSpace(imageUrl))
            return null!;

        if (_cache.TryGetValue(imageUrl, out var cachedImage))
            return cachedImage;

        ImageSource imageSource = IsSvg(imageUrl) ? new SvgImageSource() : new BitmapImage();

        if (IsLocalPath(imageUrl))
        {
            if (imageSource is BitmapImage bitmap)
                bitmap.UriSource = new Uri(imageUrl);
            else if (imageSource is SvgImageSource svg)
                svg.UriSource = new Uri(imageUrl);
            AddToCache(imageUrl, imageSource);
        }
        else
        {
            AddToCache(imageUrl, imageSource);
            _ = LoadImageAsync(imageUrl, imageSource);
        }

        return imageSource;
    }

    private static void AddToCache(string key, ImageSource imageSource)
    {
        _cache[key] = imageSource;
        _insertionQueue.Enqueue(key);

        while (
            _insertionQueue.Count > MaxCacheEntries && _insertionQueue.TryDequeue(out var oldest)
        )
            _cache.TryRemove(oldest, out _);
    }

    private static async Task LoadImageAsync(string url, ImageSource imageSource)
    {
        var downloadService = App.GetService<IDownloadService>();
        byte[]? bytes = await downloadService.GetImageBytesAsync(url);

        if (bytes == null)
        {
            // The purpose of assigning a value that refers to a file that does not exist is to trigger the ImageSource error event.
            var errorUri = new Uri("ms-appx:///Assets/___nonexistent___");
            if (imageSource is BitmapImage bitmap)
                bitmap.UriSource = errorUri;
            else if (imageSource is SvgImageSource svg)
                svg.UriSource = errorUri;

            _cache.TryRemove(url, out _);
            return;
        }

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
                break;
        }
    }

    private static bool IsSvg(string url)
    {
        return url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || url.EndsWith(".svgz", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalPath(string path)
    {
        return Path.IsPathRooted(path)
            && !path.StartsWith("http", StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
