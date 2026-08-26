using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// One game with one room, one character and the pendencies whose recipient the
/// generators have to work out.
/// </summary>
/// <remarks>
/// Deliberately small. The rows here exist to make the recipient rules
/// distinguishable — the character's author is not the person waited for, and
/// neither is the person who wrote the pendency down — so that a generator
/// answering the wrong one of the three cannot pass.
/// </remarks>
internal static class NotificationSeed
{
    private static Guid Id(string tail) => Guid.Parse("00000000-0000-0000-0000-0000000000" + tail);

    /// <summary>Runs the game and writes every pendency below.</summary>
    public static readonly Guid MasterId = Id("a1");

    /// <summary>Owns the character the pendencies point at.</summary>
    public static readonly Guid CharacterAuthorId = Id("a2");

    /// <summary>Named by WaitingForUserId, and owns nothing.</summary>
    public static readonly Guid WaitedForUserId = Id("a3");

    public static readonly Guid GameId = Id("b1");
    public static readonly Guid RoomId = Id("c1");
    public static readonly Guid CharacterId = Id("d1");

    /// <summary>Waits for a named user who is neither the author nor the master.</summary>
    public static readonly Guid PendencyWaitingForUserId = Id("e1");

    /// <summary>Names no user, so the character's author is the one waited for.</summary>
    public static readonly Guid PendencyWithoutWaitingUserId = Id("e2");

    /// <summary>Written by the master about the master.</summary>
    public static readonly Guid PendencyWaitingForItsAuthorId = Id("e3");

    /// <summary>Already answered by a post.</summary>
    public static readonly Guid FulfilledPendencyId = Id("e4");

    public const string GameTitle = "Проверочная игра";
    public const string RoomTitle = "Первая комната";
    public const string CharacterName = "Проверочный персонаж";
    public const string MasterUsername = "seed-master";

    /// <summary>How long the unfulfilled pendencies have been waiting.</summary>
    public const int DaysPending = 4;

    public static async Task ApplyAsync(DmDbContext context)
    {
        // Keyed on a row this seed owns rather than on a bare Any(): InitialCreate
        // ships rows of its own, and a blanket check would skip the seeder the
        // moment one of them is of the same kind.
        if (context.Users.Any(u => u.UserId == MasterId))
        {
            return;
        }

        var created = DateTimeOffset.UtcNow.AddDays(-30);

        context.Users.AddRange(
            User(MasterId, MasterUsername, created),
            User(CharacterAuthorId, "seed-author", created),
            User(WaitedForUserId, "seed-waited-for", created));
        await context.SaveChangesAsync();

        context.Set<Game>().Add(new Game
        {
            GameId = GameId,
            SerialNumber = 1,
            PublicId = "seedgame",
            MasterId = MasterId,
            Title = GameTitle,
            SystemName = "D&D 5e",
            NarrativeSetting = "Forgotten Realms",
            Info = "Seeded for the notification generators.",
            CreatedUtc = created,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            CommentsAccessMode = CommentsAccessMode.Public,
            IsRemoved = false
        });

        context.Set<Room>().Add(new Room
        {
            RoomId = RoomId,
            GameId = GameId,
            RoomNumber = 1,
            Title = RoomTitle,
            AccessType = RoomAccessType.Open,
            Type = RoomType.Chat,
            OrderNumber = 1.0,
            IsRemoved = false
        });

        context.Set<Character>().Add(new Character
        {
            CharacterId = CharacterId,
            GameId = GameId,
            AuthorId = CharacterAuthorId,
            Name = CharacterName,
            Status = CharacterStatus.Active,
            IsNpc = false,
            AccessPolicy = CharacterAccessPolicy.NoAccess,
            CreatedUtc = created,
            IsRemoved = false
        });
        await context.SaveChangesAsync();

        var pending = DateTimeOffset.UtcNow.AddDays(-DaysPending);

        context.Set<PostPendency>().AddRange(
            Pendency(PendencyWaitingForUserId, WaitedForUserId, pending, null),
            Pendency(PendencyWithoutWaitingUserId, null, pending, null),
            Pendency(PendencyWaitingForItsAuthorId, MasterId, pending, null),
            Pendency(FulfilledPendencyId, WaitedForUserId, pending, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    /// <summary>A registered account, filled in with whatever the columns demand.</summary>
    public static User User(Guid id, string username, DateTimeOffset created) => new()
    {
        UserId = id,
        Username = username,
        Email = username + "@example.com",
        PasswordHash = "fakehash",
        Salt = "fakesalt",
        PasswordHashVersion = 2,
        Role = UserRole.RegularUser,
        CreatedUtc = created,
        LastActivityUtc = DateTimeOffset.UtcNow,
        IsRemoved = false,
        Status = string.Empty,
        Name = string.Empty,
        Location = string.Empty,
        Info = string.Empty
    };

    private static PostPendency Pendency(
        Guid id, Guid? waitingForUserId, DateTimeOffset created, DateTimeOffset? fulfilled)
    {
        return new PostPendency
        {
            PendencyId = id,
            RoomId = RoomId,
            CharacterId = CharacterId,
            WaitingForUserId = waitingForUserId,
            CreatedById = MasterId,
            CreatedUtc = created,
            FulfilledUtc = fulfilled
        };
    }
}
