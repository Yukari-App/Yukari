using Yukari.Enums;

namespace Yukari.Models.Settings
{
    public class AppSettings
    {
        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public ReadingMode ReadingMode { get; set; } = ReadingMode.RightToLeft;
        public ScalingMode ScalingMode { get; set; } = ScalingMode.FitScreen;

        public string? DefaultComicSourceName { get; set; }
        public WindowState MainWindowState { get; set; } = new();
    }
}
