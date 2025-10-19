using System.Collections.Generic;
using Yukari.Core.Models;

namespace Yukari.Messages
{
    public record RequestFiltersDialogMessage(IReadOnlyList<Filter> Filters, IReadOnlyDictionary<string, IReadOnlyList<string>> AppliedFilters);
}
