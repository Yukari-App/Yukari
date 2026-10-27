using System;

namespace Yukari.Exceptions;

public class CollectionAlreadyExistsException : Exception
{
    public string CollectionName { get; }

    public CollectionAlreadyExistsException(string name)
        : base($"A collection named '{name}' already exists.")
    {
        CollectionName = name;
    }
}
