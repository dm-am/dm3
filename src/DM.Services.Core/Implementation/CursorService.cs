using System;
using System.Text;
using System.Text.Json;
using DM.Services.Core.Dto;

namespace DM.Services.Core.Implementation;

/// <inheritdoc />
public class CursorService : ICursorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public string Encode(Guid entityId, DateTimeOffset timestampUtc, CursorDirection direction)
    {
        var data = new CursorData
        {
            EntityId = entityId,
            TimestampUtc = timestampUtc,
            Direction = direction
        };

        var json = JsonSerializer.Serialize(data, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    /// <inheritdoc />
    public CursorData? Decode(string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<CursorData>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public bool TryDecode(string cursor, out CursorData data)
    {
        data = Decode(cursor)!;
        return data != null;
    }

    /// <inheritdoc />
    public string CreateAfterCursor(Guid entityId, DateTimeOffset timestampUtc)
    {
        return Encode(entityId, timestampUtc, CursorDirection.After);
    }

    /// <inheritdoc />
    public string CreateBeforeCursor(Guid entityId, DateTimeOffset timestampUtc)
    {
        return Encode(entityId, timestampUtc, CursorDirection.Before);
    }
}
