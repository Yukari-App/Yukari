namespace Yukari.Models
{
    public class MangaSourceModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string IconUrl { get; set; }
        public string SourceUrl { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
