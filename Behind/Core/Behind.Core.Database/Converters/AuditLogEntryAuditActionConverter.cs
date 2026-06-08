using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Behind.Core.Database.Converters;

public sealed class AuditLogEntryAuditActionConverter()
    : ValueConverter<AuditLogEntry.AuditAction, string>(
        action => AuditLogEntryAuditActionJsonConverter.ToStorageValue(action),
        value => AuditLogEntryAuditActionJsonConverter.FromStorageValue(value));