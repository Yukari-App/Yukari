namespace Yukari.Models
{
    public class MangaSourceModel
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
        public string DllPath { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
