namespace Yukari.Models.DTO
{
    public record ReaderNavigationArgs(
        ContentKey ComicKey,
        string ComicTitle,
        ContentKey ChapterKey,
        string SelectedLang
    );
}
