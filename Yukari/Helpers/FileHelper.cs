using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yukari.Helpers;

public static class FileHelper
{
    private static readonly HashSet<char> InvalidFileNameChars =
    [
        .. Path.GetInvalidFileNameChars(),
    ];

    public static string SanitizeFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;
        return string.Concat(fileName.Where(c => !InvalidFileNameChars.Contains(c)));
    }
}
