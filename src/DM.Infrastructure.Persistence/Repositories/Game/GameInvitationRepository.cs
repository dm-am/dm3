using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Shared.Users;
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

    #region Users

    /// <inheritdoc />
    public async Task<IEnumerable<GameUser>> GetUsers(Guid gameId, CancellationToken ct = default)
    {
        var users = new List<GameUser>();

        // Get master
        var master = await _dbContext.Games
            .TagWith("DM.GameInvitation.GetMaster")
            .Where(g => g.GameId == gameId && !g.IsRemoved)
            .Select(g => new GameUser
            {
                User = new GeneralUser
                {
                    UserId = g.Master.UserId,
                    Username = g.Master.Username,
                    Role = g.Master.Role,
                    Status = g.Master.Status,
                    LastActivityUtc = g.Master.LastActivityUtc,
                    Picture = AvatarProjections.From(g.Master.AvatarUpload),
                },
                Role = GameRole.Master,
                JoinedUtc = g.CreatedUtc
            })
            .FirstOrDefaultAsync(ct);

        if (master != null)
        {
            users.Add(master);
        }

        // Get mentor
        var mentor = await _dbContext.Games
            .TagWith("DM.GameInvitation.GetMentor")
            .Where(g => g.GameId == gameId && !g.IsRemoved && g.MentorId != null)
            .Select(g => new GameUser
            {
                User = new GeneralUser
                {
                    UserId = g.Mentor!.UserId,
                    Username = g.Mentor.Username,
                    Role = g.Mentor.Role,
                    Status = g.Mentor.Status,
                    LastActivityUtc = g.Mentor.LastActivityUtc,
                    Picture = AvatarProjections.From(g.Mentor.AvatarUpload),
                },
                Role = GameRole.Mentor,
                JoinedUtc = g.CreatedUtc
            })
            .FirstOrDefaultAsync(ct);

        if (mentor != null)
        {
            users.Add(mentor);
        }

        // Get assistants
        var assistants = await _dbContext.GameAssistants
            .TagWith("DM.GameInvitation.GetAssistants")
            .Where(ga => ga.GameId == gameId)
            .Select(ga => new GameUser
            {
                User = new GeneralUser
                {
                    UserId = ga.User.UserId,
                    Username = ga.User.Username,
                    Role = ga.User.Role,
                    Status = ga.User.Status,
                    LastActivityUtc = ga.User.LastActivityUtc,
                    Picture = AvatarProjections.From(ga.User.AvatarUpload),
                },
                Role = GameRole.Assistant,
                JoinedUtc = ga.JoinedUtc
            })
            .ToListAsync(ct);

        users.AddRange(assistants);

        // Get players (users with active characters)
        var players = await _dbContext.Characters
            .TagWith("DM.GameInvitation.GetPlayers")
            .Where(c => c.GameId == gameId && !c.IsRemoved && c.Status == CharacterStatus.Active && !c.IsNpc)
            .Select(c => new GameUser
            {
                User = new GeneralUser
                {
                    UserId = c.Author!.UserId,
                    Username = c.Author.Username,
                    Role = c.Author.Role,
                    Status = c.Author.Status,
                    LastActivityUtc = c.Author.LastActivityUtc,
                    Picture = AvatarProjections.From(c.Author.AvatarUpload),
                },
                Role = GameRole.Player,
                JoinedUtc = c.CreatedUtc,
                CharacterId = c.CharacterId,
                CharacterName = c.Name,
                CharacterStatus = c.Status
            })
            .ToListAsync(ct);

        users.AddRange(players);

        // Get readers (subscribers)
        var readers = await _dbContext.Subscriptions
            .TagWith("DM.GameInvitation.GetReaders")
            .Where(s => s.TargetType == SubscriptionTargetType.Game && s.TargetId == gameId)
            .Select(s => new GameUser
            {
                User = new GeneralUser
                {
                    UserId = s.Subscriber.UserId,
                    Username = s.Subscriber.Username,
                    Role = s.Subscriber.Role,
                    Status = s.Subscriber.Status,
                    LastActivityUtc = s.Subscriber.LastActivityUtc,
                    Picture = AvatarProjections.From(s.Subscriber.AvatarUpload),
                },
                Role = GameRole.Reader,
                JoinedUtc = s.CreatedUtc
            })
            .ToListAsync(ct);

        // Filter out readers who are also players/assistants/master
        var existingUserIds = users.Select(u => u.User.UserId).ToHashSet();
        users.AddRange(readers.Where(r => !existingUserIds.Contains(r.User.UserId)));

        return users;
    }

    /// <inheritdoc />
    public async Task AddAssistant(AddAssistantEntity entity, CancellationToken ct = default)
    {
        var assistant = new GameAssistant
        {
            GameAssistantId = entity.GameAssistantId,
            GameId = entity.GameId,
            UserId = entity.UserId,
            JoinedUtc = entity.JoinedUtc
        };

        _dbContext.GameAssistants.Add(assistant);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task RemoveAssistant(Guid gameId, Guid userId, CancellationToken ct = default)
    {
        await _dbContext.GameAssistants
            .Where(ga => ga.GameId == gameId && ga.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public async Task UpdateMaster(UpdateMasterEntity entity, CancellationToken ct = default)
    {
        // Add old master as assistant
        var assistant = new GameAssistant
        {
            GameAssistantId = entity.NewAssistantId,
            GameId = entity.GameId,
            UserId = entity.OldMasterId,
            JoinedUtc = entity.AssistantJoinedUtc
        };

        _dbContext.GameAssistants.Add(assistant);

        // Remove new master from assistants if they were one
        await _dbContext.GameAssistants
            .Where(ga => ga.GameId == entity.GameId && ga.UserId == entity.NewMasterId)
            .ExecuteDeleteAsync(ct);

        // Update game master
        await _dbContext.Games
            .Where(g => g.GameId == entity.GameId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(g => g.MasterId, entity.NewMasterId), ct);
    }

    #endregion

    #region Invitations

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

    private static readonly System.Linq.Expressions.Expression<Func<Token, GameInvitation>> GameInvitationProjection =
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
                LastActivityUtc = t.User.LastActivityUtc
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
        var token = await _dbContext.Tokens
            .TagWith("DM.GameInvitation.Get")
            .Include(t => t.User)
            .Include(t => t.Creator)
            .Include(t => t.Game)
            .ThenInclude(g => g!.Master)
            .FirstOrDefaultAsync(t => t.TokenId == tokenId && !t.IsRemoved, ct);

        if (token == null)
            return (null, null);

        var tokenDto = new GameInvitationToken
        {
            TokenId = token.TokenId,
            UserId = token.UserId,
            TokenType = token.Type,
            EntityId = token.EntityId,
            CreatedUtc = token.CreatedUtc,
            IsValid = !token.IsRemoved
        };

        var info = new GameInvitation
        {
            TokenId = token.TokenId,
            GameId = token.EntityId ?? Guid.Empty,
            GameTitle = token.Game?.Title ?? string.Empty,
            InvitedUser = MapToGeneralUser(token.User),
            InvitedBy = token.Creator != null
                ? MapToGeneralUser(token.Creator)
                : MapToGeneralUser(token.Game!.Master),
            TargetRole = MapTokenTypeToRole(token.Type),
            CreatedUtc = token.CreatedUtc,
            ExpiresUtc = token.CreatedUtc.AddDays(InvitationPolicy.ExpirationDays)
        };

        return (tokenDto, info);
    }

    /// <inheritdoc />
    public async Task<GameInvitationToken> CreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default)
    {
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

        _dbContext.Tokens.Add(token);
        await _dbContext.SaveChangesAsync(ct);

        return new GameInvitationToken
        {
            TokenId = token.TokenId,
            UserId = token.UserId,
            TokenType = token.Type,
            EntityId = token.EntityId,
            CreatedUtc = token.CreatedUtc,
            IsValid = true
        };
    }

    /// <inheritdoc />
    public async Task<GameInvitationToken> InvalidateAndCreateInvitation(CreateGameInvitationEntity entity, CancellationToken ct = default)
    {
        // Invalidate existing invitations of same type for this user
        await _dbContext.Tokens
            .Where(t => t.EntityId == entity.GameId &&
                       t.UserId == entity.UserId &&
                       t.Type == entity.TokenType &&
                       !t.IsRemoved)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRemoved, true), ct);

        return await CreateInvitation(entity, ct);
    }

    /// <inheritdoc />
    public async Task RemoveInvitation(Guid tokenId, CancellationToken ct = default)
    {
        await _dbContext.Tokens
            .Where(t => t.TokenId == tokenId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRemoved, true), ct);
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
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsRemoved, true), ct);
        }

        return pendingInvitations.Select(t => new CancelledInvitation
        {
            TokenId = t.TokenId,
            TokenType = t.Type
        });
    }

    #endregion

    #region Mapping

    private static GeneralUser MapToGeneralUser(User user) => new()
    {
        UserId = user.UserId,
        Username = user.Username,
        Role = user.Role,
        Status = user.Status,
        LastActivityUtc = user.LastActivityUtc,
        Picture = AvatarProjections.From(user.AvatarUpload),
    };

    private static GameRole MapTokenTypeToRole(TokenType type) => type switch
    {
        TokenType.GamePlayerInvitation => GameRole.Player,
        TokenType.GameReaderInvitation => GameRole.Reader,
        TokenType.GameAssistantInvitation => GameRole.Assistant,
        _ => GameRole.None
    };

    #endregion
}
