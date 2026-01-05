using Dapper;
using System;
using System.Data;
using System.Text.Json;
using Yukari.Models;

namespace Yukari.Helpers.Database
{
    public class LanguageArrayHandler : SqlMapper.TypeHandler<LanguageModel[]>
    {
        public override void SetValue(IDbDataParameter parameter, LanguageModel[]? value)
            => parameter.Value = JsonSerializer.Serialize(value ?? Array.Empty<LanguageModel>());

        public override LanguageModel[] Parse(object value)
            => JsonSerializer.Deserialize<LanguageModel[]>(value?.ToString() ?? "[]") ?? Array.Empty<LanguageModel>();
    }
}
