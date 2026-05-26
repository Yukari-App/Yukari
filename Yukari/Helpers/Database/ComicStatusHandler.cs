using System;
using System.Data;
using Dapper;
using Yukari.Core.Models;

namespace Yukari.Helpers.Database;

public class ComicStatusHandler : SqlMapper.TypeHandler<ComicStatus>
{
    public override ComicStatus Parse(object value) =>
        Enum.TryParse<ComicStatus>((string)value, ignoreCase: true, out var result)
            ? result
            : ComicStatus.Unknown;

    public override void SetValue(IDbDataParameter parameter, ComicStatus value) =>
        parameter.Value = value.ToString().ToLowerInvariant();
}
