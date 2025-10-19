using System.Collections.Generic;

namespace Yukari.Messages
{
    public record FiltersDialogResultMessage(Dictionary<string, IReadOnlyList<string>> AppliedFilters);
}
