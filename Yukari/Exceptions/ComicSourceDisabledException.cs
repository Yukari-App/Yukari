using System;

namespace Yukari.Exceptions;

public class ComicSourceDisabledException : Exception
{
    public string SourceName { get; }

    public ComicSourceDisabledException(string sourceName)
        : base(
            $"The source '{sourceName}' is currently disabled. Please enable it in the settings."
        )
    {
        SourceName = sourceName;
    }
}
