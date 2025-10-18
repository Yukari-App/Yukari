using System.Collections.Generic;
using Yukari.Core.Models;

namespace Yukari.Messages
{
    public record RequestFiltersDialogMessage(IEnumerable<Filter> Filters);
}
