using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yukari.Core.Models;

namespace Yukari.Services.UI
{
    public interface IDialogService
    {
        void Initialize(XamlRoot root);
        Task<Dictionary<string, List<string>>?> ShowFiltersDialogAsync(IEnumerable<Filter> filters);
    }
}
