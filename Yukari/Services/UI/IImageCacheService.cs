using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;

namespace Yukari.Services.UI;

public interface IImageCacheService
{
    ImageSource GetImageSource(string? url);
    bool TryGetCached(string? url, out ImageSource? source);
    Task<(int Width, int Height)?> TryGetDimensionsAsync(string? url);
}
