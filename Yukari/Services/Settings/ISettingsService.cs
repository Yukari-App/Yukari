using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Yukari.Models.Settings;

namespace Yukari.Services.Settings
{
    public interface ISettingsService
    {
        AppSettings Current { get; }

        void Set<T>(Expression<Func<AppSettings, T>> selector, T value);

        Task SaveAsync();
        Task LoadAsync();
        void Reset();

        event EventHandler<SettingsChangedEventArgs> SettingChanged;
    }

    public class SettingsChangedEventArgs : EventArgs
    {
        public required string PropertyName { get; init; }
        public object? OldValue { get; init; }
        public object? NewValue { get; init; }
    }
}
