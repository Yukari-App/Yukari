using System.Collections.Generic;

namespace Yukari.Messages
{
    public record FiltersDialogResultMessage(IReadOnlyDictionary<string, IReadOnlyList<string>> AppliedFilters);
}
