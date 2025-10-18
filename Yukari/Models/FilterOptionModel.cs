namespace Yukari.Models
{
    public class FilterOptionModel
    {
        string Key { get; set; }
        string DisplayName { get; set; }
        bool IsSelected { get; set; } = false;
    }
}
