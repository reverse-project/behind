using System.Data.Common;
using System.Text.Json;
using Behind.Common;
using Behind.Common.Database.Raw;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Commands;

public sealed class RawDatabaseCommands(
    AbstractCoreContext context,
    IAuditActorProvider actors,
    TimeProvider timeProvider) : IRawDatabaseCommands
{
    public Task<RawWriteResult> Insert(
        string table,
        IReadOnlyDictionary<string, JsonElement> values,
        CancellationToken cancellationToken = default) =>
        Write(
            table,
            cancellationToken,
            raw => raw.ParseValues(values, true)
                .Bind(parsed => raw.WithGeneratedKeys(parsed))
                .Match(
                    errors => Rejection(errors),
                    parsed => InsertRow(raw, parsed, cancellationToken)));

    public Task<RawWriteResult> Update(
        string table,
        IReadOnlyDictionary<string, JsonElement> key,
        IReadOnlyDictionary<string, JsonElement> values,
        CancellationToken cancellationToken = default) =>
        Write(
            table,
            cancellationToken,
            raw => values.Count == 0
                ? Rejection([Error("No values to change.")])
                : raw.ParseKey(key).Match(
                    errors => Rejection(errors),
                    parsedKey => raw.ParseValues(values, false).Match(
                        errors => Rejection(errors),
                        changes => UpdateRow(raw, parsedKey, changes, cancellationToken))));

    public Task<RawWriteResult> Delete(
        string table,
        IReadOnlyDictionary<string, JsonElement> key,
        CancellationToken cancellationToken = default) =>
        Write(
            table,
            cancellationToken,
            raw => raw.ParseKey(key).Match(
                errors => Rejection(errors),
                parsedKey => DeleteRow(raw, parsedKey, cancellationToken)));

    private static Task<RawWriteResult> Rejection(IEnumerable<Error> errors) =>
        Task.FromResult<RawWriteResult>(new RawWriteResult.Invalid([..errors.Select(error => error.ToString())]));

    private async Task<RawWriteResult> InsertRow(
        RawTable table,
        IReadOnlyList<RawValue> values,
        CancellationToken cancellationToken)
    {
        await context.InsertRawRowAsync(table, values, cancellationToken);

        var key = table.KeyColumns
            .Select(column => values.First(value => value.Column.Name == column.Name))
            .ToList();

        await Record(table, key.AuditKey, new AuditLogEntry.Created(), cancellationToken);

        return await Saved(table, key, cancellationToken);
    }

    private async Task<RawWriteResult> UpdateRow(
        RawTable table,
        IReadOnlyList<RawValue> key,
        IReadOnlyList<RawValue> changes,
        CancellationToken cancellationToken)
    {
        var before = await context.FindRawRowAsync(table, key, cancellationToken);

        return await before.Match<Task<RawWriteResult>>(
            () => Task.FromResult<RawWriteResult>(new RawWriteResult.NotFound()),
            async existing =>
            {
                await context.UpdateRawRowAsync(table, key, changes, cancellationToken);

                var after = await context.FindRawRowAsync(table, key, cancellationToken);

                return await after.Match<Task<RawWriteResult>>(
                    () => Task.FromResult<RawWriteResult>(new RawWriteResult.NotFound()),
                    async updated =>
                    {
                        var differences = table.DifferencesBetween(existing, updated);

                        if (differences.Count > 0)
                        {
                            await Record(table, key.AuditKey, new AuditLogEntry.Updated(differences), cancellationToken);
                        }

                        return new RawWriteResult.Saved(table.Mask(updated));
                    });
            });
    }

    private async Task<RawWriteResult> DeleteRow(
        RawTable table,
        IReadOnlyList<RawValue> key,
        CancellationToken cancellationToken)
    {
        if (!await context.DeleteRawRowAsync(table, key, cancellationToken))
        {
            return new RawWriteResult.NotFound();
        }

        await Record(table, key.AuditKey, new AuditLogEntry.HardDeleted(), cancellationToken);

        return new RawWriteResult.Deleted();
    }

    private async Task<RawWriteResult> Saved(
        RawTable table,
        IReadOnlyList<RawValue> key,
        CancellationToken cancellationToken) =>
        (await context.FindRawRowAsync(table, key, cancellationToken)).Match<RawWriteResult>(
            () => new RawWriteResult.NotFound(),
            row => new RawWriteResult.Saved(table.Mask(row)));

    private async Task Record(
        RawTable table,
        string key,
        AuditLogEntry.AuditAction action,
        CancellationToken cancellationToken)
    {
        context.AddAuditEntry(
            new AuditLogEntry(table.EntityName, key, action, timeProvider.GetUtcNow()),
            await actors.GetCurrentAsync(cancellationToken));

        await context.SaveChangesAsync(cancellationToken);
    }

    private Task<RawWriteResult> Write(
        string name,
        CancellationToken cancellationToken,
        Func<RawTable, Task<RawWriteResult>> write) =>
        context.FindRawTable(name).Match(
            () => Task.FromResult<RawWriteResult>(new RawWriteResult.NotFound()),
            table => context.IsReadOnly(table)
                ? Task.FromResult<RawWriteResult>(new RawWriteResult.ReadOnly())
                : Transacted(() => write(table), cancellationToken));

    private async Task<RawWriteResult> Transacted(
        Func<Task<RawWriteResult>> write,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var result = await write();
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbException exception)
        {
            return new RawWriteResult.Rejected(exception.Message);
        }
    }
}

internal static class RawRowDifferenceExtensions
{
    extension(RawTable table)
    {
        public IReadOnlyList<PropertyDifference> DifferencesBetween(RawRow before, RawRow after) =>
        [
            ..table.Columns
                .Where(column => !Equals(before.Values[column.Name], after.Values[column.Name]))
                .Select(column => new PropertyDifference(
                    column.Name,
                    column.Present(before.Values[column.Name]),
                    column.Present(after.Values[column.Name])))
        ];
    }
}
