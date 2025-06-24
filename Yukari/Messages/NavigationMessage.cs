using System;

namespace Yukari.Messages
{
    public record NavigationMessage(Type PageType, object? Parameter);
}