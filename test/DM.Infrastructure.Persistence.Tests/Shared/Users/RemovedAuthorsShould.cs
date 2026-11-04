using System;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Infrastructure.Persistence.Tests.Shared.Users;

/// <summary>
/// A content read lifts the account filter and nothing else, and the nested user it
/// then reaches is nobody when the account is removed.
/// </summary>
/// <remarks>The lift keeps the rows a removed account wrote; the formula keeps the account out of the answer.</remarks>
public class RemovedAuthorsShould
{

    private static readonly Func<DbUser?, DM.Domain.Core.Dto.GeneralUser> Nested =
        ExpressionSplicer.Expand(GeneralUserProjections.Projection).Compile();

    private static DbUser Account(bool isRemoved) => new()
    {
        UserId = Guid.NewGuid(),
        Username = "Эльвира",
        Email = "elvira@example.com",
        PasswordHash = "hash",
        Salt = "salt",
        IsRemoved = isRemoved,
    };

    [Fact]
    public void NestARemovedAccountAsNobody()
    {
        Nested(Account(isRemoved: true)).Should().BeNull(
            "the content reads reach the removed row, and this formula is what keeps its " +
            "name, address and avatar out of the answer");
    }

    [Fact]
    public void NestALiveAccountAsItself()
    {
        Nested(Account(isRemoved: false)).Username.Should().Be("Эльвира");
    }

    [Fact]
    public void LiftTheAccountFilterAndKeepEveryOther()
    {
        using var probe = ProbeContext.Open();

        var sql = probe.Posts
            .KeepingRemovedAuthors()
            .Select(p => new { p.GameText, p.Author.Username, Room = p.Room.Title })
            .ToQueryString();

        Regex.IsMatch(sql, @"FROM ""Users"" AS (\w+)\s+WHERE NOT \(\1\.""IsRemoved""\)").Should().BeFalse(
            "the account filter is the one lifted, so the join reaches every author");
        Regex.IsMatch(sql, @"WHERE NOT \((\w+)\.""IsRemoved""\)\s*$", RegexOptions.Multiline).Should().BeTrue(
            "a removed post stays removed: only the account filter is lifted, the post's is not");
        Regex.IsMatch(sql, @"FROM ""Rooms"" AS (\w+)\s+WHERE NOT \(\1\.""IsRemoved""\)").Should().BeTrue(
            "a removed room stays removed: only the account filter is lifted, the room's is not");
    }
}
