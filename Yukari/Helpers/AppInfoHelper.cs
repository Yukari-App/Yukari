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
    }
}
