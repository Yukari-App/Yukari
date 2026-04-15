using System;

namespace Yukari.Models.DTO
{
    public class SettingsChangedEventArgs : EventArgs
    {
        public required string PropertyName { get; init; }
        public object? OldValue { get; init; }
        public object? NewValue { get; init; }
    }
}
