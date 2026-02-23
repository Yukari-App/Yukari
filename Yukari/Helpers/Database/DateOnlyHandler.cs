using System;
using System.Data;
using Dapper;

public class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
        parameter.Value = value.ToString("yyyy-MM-dd");

    public override DateOnly Parse(object value) => DateOnly.Parse(value.ToString()!);
}
