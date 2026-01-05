using Dapper;
using System;
using System.Data;
using System.Text.Json;

namespace Yukari.Helpers.Database
{
    public class JsonStringArrayHandler : SqlMapper.TypeHandler<string[]>
    {
        public override void SetValue(IDbDataParameter parameter, string[]? value)
            => parameter.Value = JsonSerializer.Serialize(value ?? Array.Empty<string>());

        public override string[] Parse(object value)
            => JsonSerializer.Deserialize<string[]>(value?.ToString() ?? "[]") ?? Array.Empty<string>();
    }
}
