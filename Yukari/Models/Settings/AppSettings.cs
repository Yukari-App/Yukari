using Yukari.Enums;

namespace Yukari.Models.Settings;

public class AppSettings
{
    public WindowState MainWindowState { get; set; } = new();
    public bool NavigationPaneIsOpen { get; set; } = true;
    public ThemeMode Theme { get; set; } = ThemeMode.System;

    public FavoritesSortBy FavoritesSortBy { get; set; } = FavoritesSortBy.Alphabetical;
    public SortDirection FavoritesSortDirection { get; set; } = SortDirection.Ascending;

    public bool ReversedChaptersOrder { get; set; } = false;

    public bool AutoFullscreen { get; set; } = false;
    public ReadingMode ReadingMode { get; set; } = ReadingMode.RightToLeft;
    public ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;

    public string? DefaultComicSourceName { get; set; }
}
