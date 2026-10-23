using Yukari.Models.Data;

namespace Yukari.Models.DTO;

public record ChapterAggregate(ChapterModel Chapter, ChapterUserData UserData)
{
    public ContentKey Key => Chapter.Key;
    public bool IsReadable => Chapter.IsReadable(UserData.IsDownloaded);
}
