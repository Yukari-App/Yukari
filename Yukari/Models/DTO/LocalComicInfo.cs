namespace Yukari.Models.DTO;

public record LocalComicInfo(
    string Title,
    string? Author,
    string? Description,
    string[] Tags,
    int? Year,
    string? CoverImageUrl
);
