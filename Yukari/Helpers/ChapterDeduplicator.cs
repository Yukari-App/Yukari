using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Yukari.Models.DTO;

namespace Yukari.Helpers;

public static class ChapterDeduplicator
{
    public static IReadOnlyList<ChapterAggregate> Deduplicate(
        IReadOnlyList<ChapterAggregate> chapters
    )
    {
        if (chapters.Count <= 1)
            return chapters;

        var groupedChapters = chapters
            .Select(
                (aggregate, index) =>
                    (
                        Aggregate: aggregate,
                        OriginalIndex: index,
                        GroupKey: NormalizeChapterNumber(aggregate.Chapter.Number)
                            ?? $"__unnumbered:{index}"
                    )
            )
            .GroupBy(x => x.GroupKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groupFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var chapterGroup in groupedChapters)
        {
            var distinctGroupsInChapter = chapterGroup
                .SelectMany(x => x.Aggregate.Chapter.Groups ?? Array.Empty<string>())
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var scanGroup in distinctGroupsInChapter)
                groupFrequency[scanGroup] = groupFrequency.GetValueOrDefault(scanGroup) + 1;
        }

        return groupedChapters
            .Select(chapterGroup =>
                chapterGroup
                    .OrderByDescending(x =>
                        x.Aggregate.Chapter.IsAvailable || x.Aggregate.UserData.IsDownloaded
                    )
                    .ThenByDescending(x => x.Aggregate.UserData.IsDownloaded)
                    .ThenByDescending(x =>
                        x.Aggregate.UserData.IsRead || x.Aggregate.UserData.LastPageRead > 0
                    )
                    .ThenByDescending(x =>
                        GetMaxGroupFrequency(x.Aggregate.Chapter.Groups, groupFrequency)
                    )
                    .ThenBy(x => x.OriginalIndex)
                    .First()
                    .Aggregate
            )
            .ToList();
    }

    private static string? NormalizeChapterNumber(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
            return null;

        var trimmed = number.Trim();
        if (
            decimal.TryParse(
                trimmed,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed
            )
        )
        {
            return parsed.ToString("G29", CultureInfo.InvariantCulture);
        }

        return trimmed;
    }

    private static int GetMaxGroupFrequency(
        string[]? groups,
        IReadOnlyDictionary<string, int> groupFrequency
    )
    {
        if (groups is not { Length: > 0 })
            return 0;

        var max = 0;
        foreach (var group in groups)
        {
            if (
                !string.IsNullOrWhiteSpace(group)
                && groupFrequency.TryGetValue(group, out var freq)
                && freq > max
            )
            {
                max = freq;
            }
        }

        return max;
    }
}
