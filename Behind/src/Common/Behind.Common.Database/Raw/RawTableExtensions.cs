using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Database.Raw;

public static class RawTableExtensions
{
    private static readonly IReadOnlyDictionary<Type, RawValueKind> Kinds = new Dictionary<Type, RawValueKind>
    {
        [typeof(string)] = RawValueKind.Text,
        [typeof(bool)] = RawValueKind.Boolean,
        [typeof(byte)] = RawValueKind.Integer,
        [typeof(short)] = RawValueKind.Integer,
        [typeof(int)] = RawValueKind.Integer,
        [typeof(long)] = RawValueKind.Integer,
        [typeof(float)] = RawValueKind.Number,
        [typeof(double)] = RawValueKind.Number,
        [typeof(decimal)] = RawValueKind.Number,
        [typeof(byte[])] = RawValueKind.Binary,
        [typeof(Guid)] = RawValueKind.Guid,
        [typeof(DateTime)] = RawValueKind.DateTime,
        [typeof(DateTimeOffset)] = RawValueKind.DateTime
    };

    extension(DbContext context)
    {
        public IReadOnlyList<RawTable> RawTables =>
        [
            ..context.GetService<IDesignTimeModel>().Model.GetRelationalModel().Tables
                .Where(table => table.PrimaryKey is not null)
                .OrderBy(table => table.Name, StringComparer.Ordinal)
                .Select(table => table.ToRawTable())
        ];

        public Option<RawTable> FindRawTable(string name) =>
            context.RawTables.Find(table => string.Equals(table.Name, name, StringComparison.Ordinal));
    }

    extension(RawTable table)
    {
        public Option<RawColumn> FindColumn(string name) =>
            table.Columns.Find(column => string.Equals(column.Name, name, StringComparison.Ordinal));

        public Validation<IReadOnlyList<RawValue>> ParseKey(IReadOnlyDictionary<string, JsonElement> key) =>
            key.Keys.Where(name => table.KeyColumns.All(column => column.Name != name)).ToList() is { Count: > 0 } extra
                ? Invalid<IReadOnlyList<RawValue>>(Error($"{string.Join(", ", extra)} is not a key column of {table.Name}."))
                : Collect(table.KeyColumns.Select(column => key.TryGetValue(column.Name, out var element)
                    ? column.Parse(element).Bind(RequireValue)
                    : Invalid<RawValue>(Error($"Key column {column.Name} is required."))));

        public Validation<IReadOnlyList<RawValue>> ParseValues(
            IReadOnlyDictionary<string, JsonElement> values,
            bool includeKeys) =>
            Collect(values
                .Where(pair => !table.IsMaskedInput(pair.Key, pair.Value))
                .Select(pair => table.FindColumn(pair.Key).Match(
                    () => Invalid<RawValue>(Error($"{pair.Key} is not a column of {table.Name}.")),
                    column => column.IsKey && !includeKeys
                        ? Invalid<RawValue>(Error($"Key column {column.Name} cannot be changed."))
                        : column.Parse(pair.Value))));

        public Validation<IReadOnlyList<RawValue>> WithGeneratedKeys(IReadOnlyList<RawValue> values)
        {
            var missing = table.KeyColumns
                .Where(column => values.All(value => value.Column.Name != column.Name))
                .ToList();

            return missing.FirstOrDefault(column => column.Kind != RawValueKind.Guid) is { } required
                ? Invalid<IReadOnlyList<RawValue>>(Error($"Key column {required.Name} is required."))
                : Valid<IReadOnlyList<RawValue>>(
                    [..values, ..missing.Select(column => new RawValue(column, Guid.CreateVersion7()))]);
        }

        public RawRow Mask(RawRow row) =>
            new(table.Columns.ToDictionary(
                column => column.Name,
                column => column.Present(row.Values[column.Name])));

        private bool IsMaskedInput(string name, JsonElement element) =>
            table.FindColumn(name).Match(() => false, column => column.IsMasked(element));
    }

    extension(IReadOnlyEntityType entityType)
    {
        private bool IsMainEntity => entityType.BaseType is null;
    }

    extension(ITable table)
    {
        private RawTable ToRawTable() =>
            new(
                table.Name,
                table.Schema,
                table.EntityTypeMappings
                    .Select(mapping => mapping.TypeBase)
                    .OfType<IEntityType>()
                    .OrderBy(entityType => entityType.IsMainEntity ? 0 : 1)
                    .First()
                    .AuditEntityName,
                [..table.Columns.Select(column => column.ToRawColumn(table))]);
    }

    extension(IColumn column)
    {
        private RawColumn ToRawColumn(ITable table)
        {
            var mapping = column.PropertyMappings.First();

            return new RawColumn(
                column.Name,
                column.StoreType,
                mapping.TypeMapping.ProviderType.ToKind(),
                column.IsNullable,
                table.PrimaryKey!.Columns.Contains(column),
                column.PropertyMappings.Any(property => property.Property.PropertyInfo?.IsSensible == true));
        }
    }

    extension(RelationalTypeMapping typeMapping)
    {
        private Type ProviderType =>
            Nullable.GetUnderlyingType(typeMapping.Converter?.ProviderClrType ?? typeMapping.ClrType)
            ?? typeMapping.Converter?.ProviderClrType
            ?? typeMapping.ClrType;
    }

    extension(Type type)
    {
        private RawValueKind ToKind() =>
            Kinds.TryGetValue(type.IsEnum ? Enum.GetUnderlyingType(type) : type, out var kind)
                ? kind
                : RawValueKind.Text;
    }

    private static Validation<RawValue> RequireValue(RawValue value) =>
        value.Value is DBNull
            ? Invalid<RawValue>(Error($"Key column {value.Column.Name} cannot be null."))
            : Valid(value);

    private static Validation<IReadOnlyList<RawValue>> Collect(IEnumerable<Validation<RawValue>> parsed)
    {
        var results = parsed.ToList();
        var errors = results.SelectMany(result => result.Match(failed => failed, _ => [])).ToList();

        return errors.Count > 0
            ? Invalid<IReadOnlyList<RawValue>>(errors)
            : Valid<IReadOnlyList<RawValue>>(
                [..results.SelectMany(result => result.Match(_ => Enumerable.Empty<RawValue>(), value => [value]))]);
    }
}
