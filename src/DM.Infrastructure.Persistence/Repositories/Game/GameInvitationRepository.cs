using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Infrastructure.Persistence.Shared.Tokens;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc />
internal class GameInvitationRepository : IGameInvitationRepository
{
    private readonly DmDbContext _dbContext;

    public GameInvitationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default)
    {
        var users = new List<GameUser>();

        var master = await MasterOf(gameId, ct);
        if (master != null)
        {
            users.Add(master);
        }

        var mentor = await MentorOf(gameId, ct);
        if (mentor != null)
        {
            users.Add(mentor);
        }

        users.AddRange(await AssistantsOf(gameId, ct));
        users.AddRange(await PlayersOf(gameId, ct));

        var readers = await ReadersOf(gameId, ct);

        // Filter out readers who are also players/assistants/master
        var existingUserIds = users.Select(u => u.User.UserId).ToHashSet();
        users.AddRange(readers.Where(r => !existingUserIds.Contains(r.User.UserId)));

        return users;
    }

    private Task<GameUser?> MasterOf(Guid gameId, CancellationToken ct) => _dbContext.Games
        .TagWith("DM.GameInvitation.GetMaster")
        .Where(g => g.GameId == gameId && !g.IsRemoved)
        .SelectSpliced(g => new GameUser
        {
            User = GeneralUserProjections.RosterCard.Splice(g.Master),
            Role = GameRole.Master,
            JoinedUtc = g.CreatedUtc
        })
        .FirstOrDefaultAsync(ct);

    private Task<GameUser?> MentorOf(Guid gameId, CancellationToken ct) => _dbContext.Games
        .TagWith("DM.GameInvitation.GetMentor")
        .Where(g => g.GameId == gameId && !g.IsRemoved && g.MentorId != null)
        .SelectSpliced(g => new GameUser
        {
            User = GeneralUserProjections.RosterCard.Splice(g.Mentor!),
            Role = GameRole.Mentor,
            JoinedUtc = g.CreatedUtc
        })
        .FirstOrDefaultAsync(ct);

    private Task<List<GameUser>> AssistantsOf(Guid gameId, CancellationToken ct) => _dbContext.GameAssistants
        .TagWith("DM.GameInvitation.GetAssistants")
        .Where(ga => ga.GameId == gameId)
        .SelectSpliced(ga => new GameUser
        {
            User = GeneralUserProjections.RosterCard.Splice(ga.User),
            Role = GameRole.Assistant,
            JoinedUtc = ga.JoinedUtc
        })
        .ToListAsync(ct);

    /// <summary>Users with active characters, a row per character.</summary>
    private Task<List<GameUser>> PlayersOf(Guid gameId, CancellationToken ct) => _dbContext.Characters
        .TagWith("DM.GameInvitation.GetPlayers")
        .Where(c => c.GameId == gameId && !c.IsRemoved && c.Status == CharacterStatus.Active && !c.IsNpc)
        .SelectSpliced(c => new GameUser
        {
            User = GeneralUserProjections.RosterCard.Splice(c.Author!),
            Role = GameRole.Player,
            JoinedUtc = c.CreatedUtc,
            CharacterId = c.CharacterId,
            CharacterName = c.Name,
            CharacterStatus = c.Status
        })
        .ToListAsync(ct);

    /// <summary>Subscribers of the game.</summary>
    private Task<List<GameUser>> ReadersOf(Guid gameId, CancellationToken ct) => _dbContext.Subscriptions
        .TagWith("DM.GameInvitation.GetReaders")
        .Where(s => s.TargetType == SubscriptionTargetType.Game && s.TargetId == gameId)
        .SelectSpliced(s => new GameUser
        {
            User = GeneralUserProjections.RosterCard.Splice(s.Subscriber),
            Role = GameRole.Reader,
            JoinedUtc = s.CreatedUtc
        })
        .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<bool> AcceptAssistantInvitation(
        AddAssistantEntity entity, Guid tokenId, CancellationToken ct = default)
    {
        var spent = false;

        // Both rows or neither. Written separately, a refusal in between left the
        // invitation live next to an assistant who already has the rights it grants,
        // and accepting it a second time added the person twice.
        //
        // Through the strategy because the API host configures EnableRetryOnFailure and
        // a retrying strategy refuses a transaction opened by hand.
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            // Reset rather than left as it stands: a retry replays the whole block
            // after the first attempt rolled back, and an answer carried over from
            // that attempt would name a link this transaction never spent.
            spent = false;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // The invitation is what authorises the row below, so a link already
            // spent grants nothing. The soft-delete filter is the "still live"
            // half of the question.
            var live = await _dbContext.Tokens.AnyAsync(t => t.TokenId == tokenId, cancellation);
            if (!live) return;

            // The pair is claimed by the statement rather than by a read taken
            // before it: a read cannot see the accept running beside it, so two
            // accepts of one link both passed it, both inserted, and the unique
            // index on (GameId, UserId) refused the second with a 500 that left
            // the invitation live next to the assistant the winner had created.
            // Finding the pair already taken is the outcome this call wanted, so
            // it carries on to the only thing still owed - spending the link.
            //
            // The assistant row before the token, and never the other way round:
            // the insert takes the game row through its own foreign key, and
            // issuing an invitation takes the same row before it writes tokens,
            // so both paths reach the two tables in one order.
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "GameAssistants" ("GameAssistantId", "GameId", "UserId", "JoinedUtc")
                VALUES ({entity.GameAssistantId}, {entity.GameId}, {entity.UserId}, {entity.JoinedUtc})
                ON CONFLICT ("GameId", "UserId") DO NOTHING
                """, cancellation);

            // The answer of the whole call: the soft-delete filter is part of this
            // statement, so it matches the link once and once only. A second
            // acceptance arriving together with this one waits on the row lock,
            // re-tests the predicate against the committed state and touches
            // nothing - and its caller then knows not to announce a seat somebody
            // else granted.
            var killed = await _dbContext.Tokens
                .Where(t => t.TokenId == tokenId)
                .Retire(cancellation);

            // The seat stands on the link that grants it, so the two are written
            // or neither is. Matching nothing here means the link went out between
            // the check at the top of the block and this statement - the master
            // revoked it, or issuing a new one replaced it - and committing the
            // insert anyway would seat an assistant by a link that grants nothing,
            // without so much as an event to say it happened. Leaving the block
            // without committing takes the insert back with the transaction.
            if (killed != 1) return;

            await transaction.CommitAsync(cancellation);
            spent = true;
        }, ct);

        return spent;
    }

    /// <inheritdoc />
    public async Task RemoveAssistant(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await _dbContext.GameAssistants
            .Where(ga => ga.GameId == gameId && ga.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameInvitation>> GetPendingInvitations(Guid gameId, CancellationToken ct = default)
    {
        return await GetGameInvitationsQuery()
            .Where(t => t.EntityId == gameId)
            .Select(GameInvitationProjection)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GameInvitation>> GetUserInvitations(Guid userId, CancellationToken ct = default)
    {
        return await GetGameInvitationsQuery()
            .Where(t => t.UserId == userId)
            .Select(GameInvitationProjection)
            .ToListAsync(ct);
    }

    private IQueryable<Token> GetGameInvitationsQuery()
    {
        return _dbContext.Tokens
            .TagWith("DM.GameInvitation.Query")
            .Where(t => !t.IsRemoved &&
                (t.Type == TokenType.GameAssistantInvitation ||
                 t.Type == TokenType.GamePlayerInvitation ||
                 t.Type == TokenType.GameReaderInvitation));
    }

    private static readonly Expression<Func<Token, GameInvitation>> GameInvitationProjection =
        t => new GameInvitation
        {
            TokenId = t.TokenId,
            GameId = t.EntityId ?? Guid.Empty,
            GameTitle = t.Game != null ? t.Game.Title : string.Empty,
            InvitedUser = new GeneralUser
            {
                UserId = t.User.UserId,
                Username = t.User.Username,
                Role = t.User.Role,
                Status = t.User.Status,
                LastActivityUtc = t.User.LastActivityUtc,
                // The newbie mark of the reference is read from it.
                QuantityRating = t.User.QuantityRating
            },
            InvitedBy = t.Creator != null
                ? new GeneralUser
                {
                    UserId = t.Creator.UserId,
                    Username = t.Creator.Username,
                    Role = t.Creator.Role,
                    Status = t.Creator.Status,
                    LastActivityUtc = t.Creator.LastActivityUtc
                }
                : new GeneralUser
                {
                    UserId = t.Game!.Master.UserId,
                    Username = t.Game.Master.Username,
                    Role = t.Game.Master.Role,
                    Status = t.Game.Master.Status,
                    LastActivityUtc = t.Game.Master.LastActivityUtc
                },
            TargetRole = t.Type == TokenType.GamePlayerInvitation ? GameRole.Player :
                         t.Type == TokenType.GameReaderInvitation ? GameRole.Reader :
                         t.Type == TokenType.GameAssistantInvitation ? GameRole.Assistant : GameRole.None,
            CreatedUtc = t.CreatedUtc,
            ExpiresUtc = t.CreatedUtc.AddDays(InvitationPolicy.ExpirationDays)
        };

    /// <inheritdoc />
    public async Task<(GameInvitationToken? Token, GameInvitation? Info)> GetInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var found = await _dbContext.Tokens
            .TagWith("DM.GameInvitation.Get")
            .Where(t => t.TokenId == tokenId && !t.IsRemoved)
            .SelectSpliced(t => new
            {
                Token = new GameInvitationToken
                {
                    TokenId = t.TokenId,
                    UserId = t.UserId,
                    TokenType = t.Type,
                    EntityId = t.EntityId,
                    CreatedUtc = t.CreatedUtc
                },
                Info = GameInvitationProjection.Splice(t)
            })
            .FirstOrDefaultAsync(ct);

        return found == null ? (null, null) : (found.Token, found.Info);
    }

    /// <inheritdoc />
    public async Task<GameInvitationToken> InvalidateAndCreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default)
    {
        // Both writes or neither, or the person is left with no live invitation.
        var token = new Token
        {
            TokenId = entity.TokenId,
            UserId = entity.UserId,
            EntityId = entity.GameId,
            Type = entity.TokenType,
            CreatorId = entity.CreatorId,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };
        await RetryableWrite.Run(_dbContext, async cancellation =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellation);

            // Locking the game row first serializes the invitations of one game: under
            // READ COMMITTED two concurrent sends would each invalidate nothing and both issue.
            await _dbContext.Database.ExecuteSqlRawAsync(
                """SELECT "GameId" FROM "Games" WHERE "GameId" = {0} FOR UPDATE""",
                [entity.GameId], cancellation);

            // Invalidate existing invitations of same type for this user
            await _dbContext.Tokens
                .Where(t => t.EntityId == entity.GameId &&
                           t.UserId == entity.UserId &&
                           t.Type == entity.TokenType &&
                           !t.IsRemoved)
                .Retire(cancellation);

            _dbContext.Tokens.Add(token);
            await _dbContext.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }, ct);

        return new GameInvitationToken
        {
            TokenId = token.TokenId,
            UserId = token.UserId,
            TokenType = token.Type,
            EntityId = token.EntityId,
            CreatedUtc = token.CreatedUtc
        };
    }

    /// <inheritdoc />
    public async Task<bool> RemoveInvitation(Guid tokenId, CancellationToken ct = default)
    {
        // The soft-delete filter is part of the statement, so it matches a live
        // link once and a spent one never: the count is what tells the caller
        // whether this call is the one that spent it.
        return await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId)
            .Retire(ct) > 0;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CancelledInvitation>> CancelInvitationsForUser(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        var pendingInvitations = await _dbContext.Tokens
            .TagWith("DM.GameInvitation.CancelForUser")
            .Where(t => !t.IsRemoved &&
                        t.EntityId == gameId &&
                        t.UserId == userId &&
                        (t.Type == TokenType.GamePlayerInvitation ||
                         t.Type == TokenType.GameReaderInvitation ||
                         t.Type == TokenType.GameAssistantInvitation))
            .Select(t => new { t.TokenId, t.Type })
            .ToListAsync(ct);

        if (pendingInvitations.Count > 0)
        {
            var tokenIds = pendingInvitations.Select(t => t.TokenId).ToList();
            await _dbContext.Tokens
                .Where(t => tokenIds.Contains(t.TokenId))
                .Retire(ct);
        }

        return pendingInvitations.Select(t => new CancelledInvitation
        {
            TokenId = t.TokenId,
            TokenType = t.Type
        });
    }
}
