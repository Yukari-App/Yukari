using System;

namespace Yukari.Exceptions;

public class ComicSourceDisabledException : Exception
{
    public ComicSourceDisabledException(string message)
        : base(message) { }
}
