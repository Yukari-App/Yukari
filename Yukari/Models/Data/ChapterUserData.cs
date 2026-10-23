namespace Yukari.Models.Data;

public class ChapterUserData
{
    public int? LastPageRead { get; set; }
    public bool IsDownloaded { get; set; } = false;
    public bool IsRead { get; set; } = false;

    public bool HasProgress => IsRead || LastPageRead > 0;

    public int ResumePageIndex(bool forceFirstPage = false) =>
        !IsRead && !forceFirstPage && LastPageRead > 0 ? LastPageRead.Value - 1 : 0;

    public int DisplayLastPage(int? totalPages) => IsRead ? totalPages ?? 0 : LastPageRead ?? 0;

    public void RecordProgress(int currentPage, int? totalPages)
    {
        if (IsRead)
            return;
        LastPageRead = currentPage;
        IsRead = currentPage > 0 && totalPages == currentPage;
    }
}
