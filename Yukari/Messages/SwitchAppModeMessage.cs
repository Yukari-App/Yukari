using Yukari.Enums;

namespace Yukari.Messages
{
    public record SwitchAppModeMessage(AppMode appMode, object? Parameter = null);
}
