using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Behind.Common.Database.Raw;

internal sealed class RawSqlBuilder(DbContext context)
{
    private readonly ISqlGenerationHelper _helper = context.GetService<ISqlGenerationHelper>();
    private readonly List<KeyValuePair<string, object>> _parameters = [];

    public string Quote(string identifier) => _helper.DelimitIdentifier(identifier);

    public string Quote(RawTable table) => _helper.DelimitIdentifier(table.Name, table.Schema);

    public string Columns(RawTable table) => string.Join(", ", table.Columns.Select(column => Quote(column.Name)));

    public string Bind(object value)
    {
        var name = _helper.GenerateParameterName($"p{_parameters.Count}");
        _parameters.Add(new KeyValuePair<string, object>(name, value));

        return name;
    }

    public string KeyPredicate(IEnumerable<RawValue> key) =>
        string.Join(" AND ", key.Select(value => $"{Quote(value.Column.Name)} = {Bind(value.Value)}"));

    public string SearchPredicate(RawTable table, Option<string> search) =>
        search.Match(
            () => string.Empty,
            term =>
            {
                var parameter = Bind($"%{term.EscapeLike()}%");
                var predicates = table.Columns
                    .Where(column => column.IsSearchable)
                    .Select(column => $"CAST({Quote(column.Name)} AS TEXT) LIKE {parameter} ESCAPE '\\'");

                return $" WHERE ({string.Join(" OR ", predicates)})";
            });

    public string Order(RawTable table, Option<string> orderBy, bool descending)
    {
        var columns = orderBy
            .Bind(name => table.FindColumn(name))
            .Match(() => table.KeyColumns, column => [column]);
        var direction = descending ? "DESC" : "ASC";

        return string.Join(", ", columns.Select(column => $"{Quote(column.Name)} {direction}"));
    }

    public DbCommand ToCommand(string text)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = text;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();

        foreach (var (name, value) in _parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }
}

internal static class RawSqlEscapeExtensions
{
    extension(string term)
    {
        public string EscapeLike() =>
            term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}
