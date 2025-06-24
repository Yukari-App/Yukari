using System;

namespace Yukari.Messages
{
    public record NavigateMessage(Type PageType, object? Parameter);
}