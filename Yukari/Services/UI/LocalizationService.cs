using Windows.ApplicationModel.Resources;

namespace Yukari.Services.UI;

internal class LocalizationService : ILocalizationService
{
    private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();

    public string GetString(string key) => _loader.GetString(key);

    public string GetFormattedString(string key, params object[] args) =>
        string.Format(_loader.GetString(key), args);
}
