namespace Yukari.Models
{
    public class ComicSourceModel
    {
        public required string Name { get; set; }
        public required string Version { get; set; }
        public string? LogoUrl { get; set; }
        public string? Description { get; set; }
        public required string DllPath { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
