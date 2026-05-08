namespace Yukari.Models.DTO;

public record ContentKey(string Id, string Source)
{
    public override string ToString() => $"{Id}@{Source}";
};
