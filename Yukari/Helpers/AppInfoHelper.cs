using System.Reflection;

namespace Yukari.Helpers
{
    public static class AppInfoHelper
    {
        public static string Version { get; } =
            Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? "Unknown";

        public static string CoreVersion { get; } =
            typeof(Yukari.Core.Sources.IComicSource)
                .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion.Split('+')[0]
            ?? "Unknown";
    }
}
