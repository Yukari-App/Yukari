using Yukari.Enums;

namespace Yukari.Models.Settings
{
    public class AppSettings
    {
        public ThemeMode Theme { get; set; } = ThemeMode.System;

        public WindowState MainWindowState { get; set; } = new();
    }
}
