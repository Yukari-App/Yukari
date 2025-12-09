using Yukari.Models.Data;

namespace Yukari.Models.DTO
{
    public record ChapterAggregate(
            ChapterModel Chapter,
            ChapterUserData UserData
        );
}
