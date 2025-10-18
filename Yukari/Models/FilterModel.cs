using System.Collections.Generic;

namespace Yukari.Models
{
    public class FilterModel
    {
        string Key { get; set; }
        string DisplayName { get; set; }
        List<FilterOptionModel> Options { get; set; }
        bool AllowMultiple { get; set; }
    }
}
