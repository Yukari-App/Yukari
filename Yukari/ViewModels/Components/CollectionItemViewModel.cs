using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Services.Comics;
using Yukari.Services.UI;

namespace Yukari.ViewModels.Components;

public partial class CollectionItemViewModel : ObservableObject
{
    private readonly IComicService _comicService;
    private readonly INotificationService _notificationService;

    private int _maxCollectionNameLength;

    public string PersistedName { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RenameCollectionCommand))]
    public partial string TextBoxName { get; set; }

    public IRelayCommand<string> DeleteCollectionCommand { get; }

    public CollectionItemViewModel(
        IComicService comicService,
        INotificationService notificationService,
        int maxCollectionNameLength,
        string name,
        IRelayCommand<string> deleteCommand
    )
    {
        _comicService = comicService;
        _notificationService = notificationService;
        _maxCollectionNameLength = maxCollectionNameLength;

        PersistedName = name;
        TextBoxName = name;
        DeleteCollectionCommand = deleteCommand;
    }

    private bool CanRenameCollection() =>
        !string.IsNullOrWhiteSpace(TextBoxName)
        && TextBoxName.Length <= _maxCollectionNameLength
        && TextBoxName != PersistedName;

    [RelayCommand(CanExecute = nameof(CanRenameCollection))]
    private async Task RenameCollectionAsync()
    {
        var result = await _comicService.RenameCollectionAsync(PersistedName, TextBoxName);
        if (!result.IsSuccess)
        {
            _notificationService.ShowError(result.Error!, result.ErrorTitle!);
            return;
        }

        PersistedName = TextBoxName;
        RenameCollectionCommand.NotifyCanExecuteChanged();
    }
}
