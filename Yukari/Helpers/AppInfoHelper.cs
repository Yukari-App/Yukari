using System;
using System.Reflection;
using Yukari.Core.Sources;

namespace Yukari.Helpers;

public static class AppInfoHelper
{
    private static readonly Version _coreVersion = typeof(IComicSource).Assembly.GetName().Version!;

    public static string Version { get; } =
        Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "Unknown";

    public static string CoreVersionString { get; } = _coreVersion.ToString(3);
    public static Version CoreVersion => _coreVersion;
}
