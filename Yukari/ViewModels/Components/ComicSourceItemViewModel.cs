using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yukari.Models;

namespace Yukari.ViewModels.Components;

public partial class ComicSourceItemViewModel : ObservableObject
{
    private bool _isInitializing = true;

    public ComicSourceModel ComicSource { get; }

    public IRelayCommand<ComicSourceItemViewModel> DeleteCommand { get; }
    public IRelayCommand<(string Name, bool IsEnabled)> IsEnabledChangedCommand { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    public ComicSourceItemViewModel(
        ComicSourceModel comicSource,
        IRelayCommand<ComicSourceItemViewModel> deleteCommand,
        IRelayCommand<(string Name, bool IsEnabled)> isEnabledChangedCommand
    )
    {
        ComicSource = comicSource;
        IsEnabled = ComicSource.IsEnabled;

        DeleteCommand = deleteCommand;
        IsEnabledChangedCommand = isEnabledChangedCommand;
        _isInitializing = false;
    }

    private bool CanOpenReleasesPage() => !string.IsNullOrEmpty(ComicSource.ReleasesPage);

    [RelayCommand(CanExecute = nameof(CanOpenReleasesPage))]
    private async Task OpenReleasesPageAsync() =>
        await Windows.System.Launcher.LaunchUriAsync(new Uri(ComicSource.ReleasesPage!));

    partial void OnIsEnabledChanged(bool value)
    {
        if (_isInitializing)
            return;
        IsEnabledChangedCommand.Execute((ComicSource.Name, value));
    }
}
