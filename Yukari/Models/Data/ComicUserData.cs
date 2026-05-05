using System.Collections.Generic;

namespace Yukari.Models.Data;

public class ComicUserData
{
    public string? LastSelectedLang { get; set; }
    public bool IsFavorite { get; set; }
    public IReadOnlyList<string> Collections { get; set; } = [];
}
