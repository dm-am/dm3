using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Paging;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Paging;

public class CursorServiceShould
{
    private readonly CursorService cursorService = new();

    [Fact]
    public void CreateValidBeforeCursor()
    {
        var entityId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var cursor = cursorService.CreateBeforeCursor(entityId, timestamp);

        cursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateValidAfterCursor()
    {
        var entityId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var cursor = cursorService.CreateAfterCursor(entityId, timestamp);

        cursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DecodeBeforeCursor()
    {
        var entityId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var cursor = cursorService.CreateBeforeCursor(entityId, timestamp);
        var success = cursorService.TryDecode(cursor, out var data);

        success.Should().BeTrue();
        data.EntityId.Should().Be(entityId);
        data.Direction.Should().Be(CursorDirection.Before);
        data.TimestampUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DecodeAfterCursor()
    {
        var entityId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var cursor = cursorService.CreateAfterCursor(entityId, timestamp);
        var success = cursorService.TryDecode(cursor, out var data);

        success.Should().BeTrue();
        data.EntityId.Should().Be(entityId);
        data.Direction.Should().Be(CursorDirection.After);
        data.TimestampUtc.Should().BeCloseTo(timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ReturnFalseForInvalidCursor()
    {
        var success = cursorService.TryDecode("invalid_cursor", out var data);

        success.Should().BeFalse();
        data.Should().BeNull();
    }

    [Fact]
    public void ReturnFalseForNullCursor()
    {
        var success = cursorService.TryDecode(null!, out var data);

        success.Should().BeFalse();
        data.Should().BeNull();
    }

    [Fact]
    public void ReturnFalseForEmptyCursor()
    {
        var success = cursorService.TryDecode("", out var data);

        success.Should().BeFalse();
        data.Should().BeNull();
    }

    [Fact]
    public void RoundtripWithDifferentEntityIds()
    {
        var entityId1 = Guid.NewGuid();
        var entityId2 = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var cursor1 = cursorService.CreateBeforeCursor(entityId1, timestamp);
        var cursor2 = cursorService.CreateBeforeCursor(entityId2, timestamp);

        cursor1.Should().NotBe(cursor2);

        cursorService.TryDecode(cursor1, out var data1);
        cursorService.TryDecode(cursor2, out var data2);

        data1.EntityId.Should().Be(entityId1);
        data2.EntityId.Should().Be(entityId2);
    }

    [Fact]
    public void RoundtripWithDifferentTimestamps()
    {
        var entityId = Guid.NewGuid();
        var timestamp1 = DateTimeOffset.UtcNow;
        var timestamp2 = timestamp1.AddHours(1);

        var cursor1 = cursorService.CreateBeforeCursor(entityId, timestamp1);
        var cursor2 = cursorService.CreateBeforeCursor(entityId, timestamp2);

        cursor1.Should().NotBe(cursor2);

        cursorService.TryDecode(cursor1, out var data1);
        cursorService.TryDecode(cursor2, out var data2);

        data1.TimestampUtc.Should().BeCloseTo(timestamp1, TimeSpan.FromSeconds(1));
        data2.TimestampUtc.Should().BeCloseTo(timestamp2, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateDifferentCursorsForSameDataWithDifferentDirections()
    {
        var entityId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;

        var beforeCursor = cursorService.CreateBeforeCursor(entityId, timestamp);
        var afterCursor = cursorService.CreateAfterCursor(entityId, timestamp);

        beforeCursor.Should().NotBe(afterCursor);
    }

    [Fact]
    public void PreserveUtcTimezoneInformation()
    {
        var entityId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2024, 6, 15, 12, 30, 45, TimeSpan.Zero);

        var cursor = cursorService.CreateBeforeCursor(entityId, timestamp);
        var success = cursorService.TryDecode(cursor, out var data);

        success.Should().BeTrue();
        data.TimestampUtc.Offset.Should().Be(TimeSpan.Zero);
    }
}
