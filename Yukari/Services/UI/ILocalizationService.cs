namespace Yukari.Services.UI;

public interface ILocalizationService
{
    string GetString(string key);
    string GetFormattedString(string key, params object[] args);
}
