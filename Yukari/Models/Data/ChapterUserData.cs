namespace Yukari.Models.Data
{
    public class ChapterUserData
    {
        public int? LastPageRead { get; set; }
        public bool IsDownloaded { get; set; } = false;
        public bool IsRead { get; set; } = false;
    }
}
