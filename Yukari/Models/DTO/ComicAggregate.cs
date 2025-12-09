using Yukari.Models.Data;

namespace Yukari.Models.DTO
{
    public record ComicAggregate(
            ComicModel Comic,
            ComicUserData UserData
        );
}
