using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Invitations;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Blog;

/// <inheritdoc cref="IBlogInvitationRepository" />
internal class BlogInvitationRepository : IBlogInvitationRepository
{
    private readonly DmDbContext _dbContext;

    public BlogInvitationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> FindInvitations(Guid blogId, Guid userId, TokenType type, CancellationToken ct = default) =>
        await _dbContext.Tokens
            .TagWith("DM.BlogInvitation.FindInvitations")
            .Where(t => !t.IsRemoved &&
                        t.EntityId == blogId &&
                        t.UserId == userId &&
                        t.Type == type)
            .Select(t => t.TokenId)
            .ToArrayAsync(ct);

    /// <inheritdoc />
    public async Task InvalidateAndCreate(IEnumerable<Guid> invitationsToInvalidate, CreateBlogInvitationEntity entity, CancellationToken ct = default)
    {
        // Invalidate old invitations
        var tokenIds = invitationsToInvalidate.ToList();
        if (tokenIds.Count > 0)
        {
            var tokensToInvalidate = await _dbContext.Tokens
                .Where(t => tokenIds.Contains(t.TokenId))
                .ToListAsync(ct);

            foreach (var token in tokensToInvalidate)
            {
                token.IsRemoved = true;
            }
        }

        // Create new invitation token
        var newToken = new Token
        {
            TokenId = entity.TokenId,
            UserId = entity.UserId,
            EntityId = entity.BlogId,
            Type = entity.TokenType,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.Tokens.Add(newToken);
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task Invalidate(Guid tokenId, CancellationToken ct = default)
    {
        var token = await _dbContext.Tokens.FindAsync([tokenId], ct);
        if (token != null)
        {
            token.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task AcceptAssistantInvitation(
        AddBlogAssistantEntity entity, Guid tokenId, CancellationToken ct = default)
    {
        // Both rows or neither. Written separately, a refusal in between left the
        // invitation live next to an assistant who already has the rights it grants,
        // and accepting it a second time added the person twice.
        //
        // Both writes go through the tracker, so one SaveChanges covers them and no
        // explicit transaction is needed.
        var token = await _dbContext.Tokens.FindAsync([tokenId], ct);
        if (token == null) return;

        var alreadyAssistant = await _dbContext.BlogAssistants
            .AnyAsync(a => a.BlogId == entity.BlogId && a.UserId == entity.UserId, ct);

        if (!alreadyAssistant)
        {
            _dbContext.BlogAssistants.Add(new Entities.Blog.BlogAssistant
            {
                BlogId = entity.BlogId,
                UserId = entity.UserId,
                JoinedUtc = entity.JoinedUtc
            });
        }

        token.IsRemoved = true;
        await _dbContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<BlogInvitation?> GetInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var data = await ProjectInvitations(_dbContext.Tokens
                .TagWith("DM.BlogInvitation.GetInvitation")
                .Where(t => !t.IsRemoved && t.TokenId == tokenId))
            .FirstOrDefaultAsync(ct);

        return data == null ? null : ToBlogInvitation(data);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId, CancellationToken ct = default)
    {
        var data = await ProjectInvitations(_dbContext.Tokens
                .TagWith("DM.BlogInvitation.GetPendingInvitations")
                .Where(t => !t.IsRemoved &&
                            t.EntityId == blogId &&
                            (t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation)))
            .OrderByDescending(d => d.CreatedUtc)
            .ToArrayAsync(ct);

        return data.Select(ToBlogInvitation);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations(Guid userId, CancellationToken ct = default)
    {
        var data = await ProjectInvitations(_dbContext.Tokens
                .TagWith("DM.BlogInvitation.GetUserPendingInvitations")
                .Where(t => !t.IsRemoved &&
                            t.UserId == userId &&
                            (t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation)))
            .OrderByDescending(d => d.CreatedUtc)
            .ToArrayAsync(ct);

        return data.Select(ToBlogInvitation);
    }

    // Kept as explicit inner joins (not Token.Blog/User navigations): missing
    // blog or user rows must drop the token, and the inviter fallback is the
    // blog author looked up by a subquery rather than a join.
    private IQueryable<InvitationData> ProjectInvitations(IQueryable<Token> tokens) =>
        tokens
            .Join(_dbContext.Blogs,
                t => t.EntityId,
                b => b.BlogId,
                (t, b) => new { Token = t, Blog = b })
            .Join(_dbContext.Users,
                tb => tb.Token.UserId,
                u => u.UserId,
                (tb, u) => new { tb.Token, tb.Blog, InvitedUser = u })
            .Select(tbu => new InvitationData
            {
                TokenId = tbu.Token.TokenId,
                BlogId = tbu.Blog.BlogId,
                BlogTitle = tbu.Blog.Title,
                InvitedUserId = tbu.InvitedUser.UserId,
                InvitedUsername = tbu.InvitedUser.Username,
                InviterUserId = tbu.Token.CreatorId ?? tbu.Blog.AuthorId,
                InviterUsername = tbu.Token.Creator != null
                    ? tbu.Token.Creator.Username
                    : _dbContext.Users.Where(u => u.UserId == tbu.Blog.AuthorId).Select(u => u.Username).FirstOrDefault(),
                Type = tbu.Token.Type,
                CreatedUtc = tbu.Token.CreatedUtc
            });

    private static BlogInvitation ToBlogInvitation(InvitationData data) => new()
    {
        TokenId = data.TokenId,
        BlogId = data.BlogId,
        BlogTitle = data.BlogTitle,
        InvitedUser = new GeneralUser { UserId = data.InvitedUserId, Username = data.InvitedUsername },
        InvitedBy = new GeneralUser { UserId = data.InviterUserId, Username = data.InviterUsername ?? "" },
        TargetRole = data.Type == TokenType.BlogAssistantInvitation ? BlogRole.Assistant : BlogRole.Reader,
        CreatedUtc = data.CreatedUtc,
        ExpiresUtc = data.CreatedUtc.AddDays(InvitationPolicy.ExpirationDays)
    };

    private sealed class InvitationData
    {
        public Guid TokenId { get; init; }
        public Guid BlogId { get; init; }
        public string BlogTitle { get; init; } = "";
        public Guid InvitedUserId { get; init; }
        public string InvitedUsername { get; init; } = "";
        public Guid InviterUserId { get; init; }
        public string? InviterUsername { get; init; }
        public TokenType Type { get; init; }
        public DateTimeOffset CreatedUtc { get; init; }
    }
}
