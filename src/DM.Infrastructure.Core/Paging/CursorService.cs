using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Core.Paging;

/// <inheritdoc />
public class CursorService : ICursorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string Encode(Guid entityId, DateTimeOffset timestampUtc, CursorDirection direction)
    {
        var data = new CursorData
        {
            EntityId = entityId,
            TimestampUtc = timestampUtc,
            Direction = direction
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions));
    }

    private static CursorData? Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CursorData>(Convert.FromBase64String(cursor), JsonOptions);
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool TryDecode(string cursor, [NotNullWhen(true)] out CursorData? data)
    {
        data = Decode(cursor);
        return data != null;
    }

    /// <inheritdoc />
    public string CreateAfterCursor(Guid entityId, DateTimeOffset timestampUtc) =>
        Encode(entityId, timestampUtc, CursorDirection.After);

    /// <inheritdoc />
    public string CreateBeforeCursor(Guid entityId, DateTimeOffset timestampUtc) =>
        Encode(entityId, timestampUtc, CursorDirection.Before);
}
