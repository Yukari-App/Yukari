using System;
using Yukari.Models;
using Yukari.Models.DTO;

namespace Yukari.Helpers;

public static class ComicExtensions
{
    public static bool IsLocal(this ContentKey key) =>
        key.Source.Equals(LocalComicConstants.SourceName, StringComparison.OrdinalIgnoreCase);

    public static bool IsLocal(this ComicModel comic) =>
        comic.Source.Equals(LocalComicConstants.SourceName, StringComparison.OrdinalIgnoreCase);

    public static bool IsLocal(this ChapterModel chapter) =>
        chapter.Source.Equals(LocalComicConstants.SourceName, StringComparison.OrdinalIgnoreCase);
}
