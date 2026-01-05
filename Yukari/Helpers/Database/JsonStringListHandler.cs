using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;

namespace Yukari.Helpers.Database
{
    public class JsonStringListHandler : SqlMapper.TypeHandler<List<string>>
    {
        public override void SetValue(IDbDataParameter parameter, List<string>? value)
            => parameter.Value = JsonSerializer.Serialize(value ?? new List<string>());

        public override List<string> Parse(object value)
            => JsonSerializer.Deserialize<List<string>>(value?.ToString() ?? "[]") ?? new List<string>();
    }
}