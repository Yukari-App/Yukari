using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Yukari.ViewModels.Components;

namespace Yukari.Services.UI
{
    public interface IDialogService
    {
        void Initialize(XamlRoot root);
        Task<bool?> ShowFiltersDialogAsync(ObservableCollection<FilterViewModel> viewModels);
    }
}
