using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Database.Raw;

public static class RawDatabaseExtensions
{
    extension(DbContext context)
    {
        public async Task<long> CountRawRowsAsync(
            RawTable table,
            Option<string> search,
            CancellationToken cancellationToken = default)
        {
            var sql = new RawSqlBuilder(context);

            return await context.ExecuteRawAsync(
                sql.ToCommand($"SELECT COUNT(*) FROM {sql.Quote(table)}{sql.SearchPredicate(table, search)}"),
                async command => Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)));
        }

        public async Task<PagedResult<RawRow>> ListRawRowsAsync(
            RawTable table,
            RawRowQuery query,
            CancellationToken cancellationToken = default)
        {
            var total = await context.CountRawRowsAsync(table, query.Search, cancellationToken);
            var sql = new RawSqlBuilder(context);
            var text = $"SELECT {sql.Columns(table)} FROM {sql.Quote(table)}{sql.SearchPredicate(table, query.Search)} "
                       + $"ORDER BY {sql.Order(table, query.OrderBy, query.Descending)} "
                       + $"LIMIT {query.Flipper.Size} OFFSET {query.Flipper.Skip}";

            var rows = await context.ExecuteRawAsync(
                sql.ToCommand(text),
                command => command.ReadRowsAsync(table, cancellationToken));

            return new PagedResult<RawRow>(
                total,
                query.Flipper.Index,
                query.Flipper.PageCount(total),
                query.Flipper.Size,
                rows.Count > 0 ? Some<IEnumerable<RawRow>>(rows) : None);
        }

        public async Task<Option<RawRow>> FindRawRowAsync(
            RawTable table,
            IReadOnlyList<RawValue> key,
            CancellationToken cancellationToken = default)
        {
            var sql = new RawSqlBuilder(context);
            var text = $"SELECT {sql.Columns(table)} FROM {sql.Quote(table)} WHERE {sql.KeyPredicate(key)}";

            var rows = await context.ExecuteRawAsync(
                sql.ToCommand(text),
                command => command.ReadRowsAsync(table, cancellationToken));

            return rows.Head();
        }

        public async Task<bool> InsertRawRowAsync(
            RawTable table,
            IReadOnlyList<RawValue> values,
            CancellationToken cancellationToken = default)
        {
            var sql = new RawSqlBuilder(context);
            var names = string.Join(", ", values.Select(value => sql.Quote(value.Column.Name)));
            var parameters = string.Join(", ", values.Select(value => sql.Bind(value.Value)));

            return await context.ExecuteRawAsync(
                sql.ToCommand($"INSERT INTO {sql.Quote(table)} ({names}) VALUES ({parameters})"),
                async command => await command.ExecuteNonQueryAsync(cancellationToken) > 0);
        }

        public async Task<bool> UpdateRawRowAsync(
            RawTable table,
            IReadOnlyList<RawValue> key,
            IReadOnlyList<RawValue> values,
            CancellationToken cancellationToken = default)
        {
            var sql = new RawSqlBuilder(context);
            var assignments = string.Join(
                ", ",
                values.Select(value => $"{sql.Quote(value.Column.Name)} = {sql.Bind(value.Value)}"));

            return await context.ExecuteRawAsync(
                sql.ToCommand($"UPDATE {sql.Quote(table)} SET {assignments} WHERE {sql.KeyPredicate(key)}"),
                async command => await command.ExecuteNonQueryAsync(cancellationToken) > 0);
        }

        public async Task<bool> DeleteRawRowAsync(
            RawTable table,
            IReadOnlyList<RawValue> key,
            CancellationToken cancellationToken = default)
        {
            var sql = new RawSqlBuilder(context);

            return await context.ExecuteRawAsync(
                sql.ToCommand($"DELETE FROM {sql.Quote(table)} WHERE {sql.KeyPredicate(key)}"),
                async command => await command.ExecuteNonQueryAsync(cancellationToken) > 0);
        }

        private async Task<TResult> ExecuteRawAsync<TResult>(DbCommand command, Func<DbCommand, Task<TResult>> run)
        {
            await using (command)
            {
                await context.Database.OpenConnectionAsync();

                try
                {
                    return await run(command);
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
        }
    }

    extension(DbCommand command)
    {
        private async Task<IReadOnlyList<RawRow>> ReadRowsAsync(RawTable table, CancellationToken cancellationToken)
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<RawRow>();

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(table.ToRow(reader));
            }

            return rows;
        }
    }

    extension(RawTable table)
    {
        private RawRow ToRow(DbDataReader reader) =>
            new(table.Columns.Select((column, index) => KeyValuePair.Create(
                    column.Name,
                    column.Normalize(reader.IsDBNull(index) ? null : reader.GetValue(index))))
                .ToDictionary());
    }
}
