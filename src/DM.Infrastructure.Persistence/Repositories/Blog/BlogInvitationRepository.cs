using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task<BlogInvitation?> GetInvitation(Guid tokenId, CancellationToken ct = default)
    {
        var data = await _dbContext.Tokens
            .TagWith("DM.BlogInvitation.GetInvitation")
            .Where(t => !t.IsRemoved && t.TokenId == tokenId)
            .Join(_dbContext.Blogs,
                t => t.EntityId,
                b => b.BlogId,
                (t, b) => new { Token = t, Blog = b })
            .Join(_dbContext.Users,
                tb => tb.Token.UserId,
                u => u.UserId,
                (tb, u) => new { tb.Token, tb.Blog, InvitedUser = u })
            .Select(tbu => new
            {
                tbu.Token.TokenId,
                tbu.Blog.BlogId,
                BlogTitle = tbu.Blog.Title,
                InvitedUserId = tbu.InvitedUser.UserId,
                InvitedUsername = tbu.InvitedUser.Username,
                InviterUserId = tbu.Token.CreatorId ?? tbu.Blog.AuthorId,
                InviterUsername = tbu.Token.Creator != null
                    ? tbu.Token.Creator.Username
                    : _dbContext.Users.Where(u => u.UserId == tbu.Blog.AuthorId).Select(u => u.Username).FirstOrDefault(),
                tbu.Token.Type,
                tbu.Token.CreatedUtc
            })
            .FirstOrDefaultAsync(ct);

        if (data == null) return null;

        return new BlogInvitation
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
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetPendingInvitations(Guid blogId, CancellationToken ct = default)
    {
        var data = await _dbContext.Tokens
            .TagWith("DM.BlogInvitation.GetPendingInvitations")
            .Where(t => !t.IsRemoved &&
                        t.EntityId == blogId &&
                        (t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation))
            .Join(_dbContext.Blogs,
                t => t.EntityId,
                b => b.BlogId,
                (t, b) => new { Token = t, Blog = b })
            .Join(_dbContext.Users,
                tb => tb.Token.UserId,
                u => u.UserId,
                (tb, u) => new { tb.Token, tb.Blog, InvitedUser = u })
            .Select(tbu => new
            {
                tbu.Token.TokenId,
                tbu.Blog.BlogId,
                BlogTitle = tbu.Blog.Title,
                InvitedUserId = tbu.InvitedUser.UserId,
                InvitedUsername = tbu.InvitedUser.Username,
                InviterUserId = tbu.Token.CreatorId ?? tbu.Blog.AuthorId,
                InviterUsername = tbu.Token.Creator != null
                    ? tbu.Token.Creator.Username
                    : _dbContext.Users.Where(u => u.UserId == tbu.Blog.AuthorId).Select(u => u.Username).FirstOrDefault(),
                tbu.Token.Type,
                tbu.Token.CreatedUtc
            })
            .OrderByDescending(d => d.CreatedUtc)
            .ToArrayAsync(ct);

        return data.Select(d => new BlogInvitation
        {
            TokenId = d.TokenId,
            BlogId = d.BlogId,
            BlogTitle = d.BlogTitle,
            InvitedUser = new GeneralUser { UserId = d.InvitedUserId, Username = d.InvitedUsername },
            InvitedBy = new GeneralUser { UserId = d.InviterUserId, Username = d.InviterUsername ?? "" },
            TargetRole = d.Type == TokenType.BlogAssistantInvitation ? BlogRole.Assistant : BlogRole.Reader,
            CreatedUtc = d.CreatedUtc,
            ExpiresUtc = d.CreatedUtc.AddDays(InvitationPolicy.ExpirationDays)
        });
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitation>> GetUserPendingInvitations(Guid userId, CancellationToken ct = default)
    {
        var data = await _dbContext.Tokens
            .TagWith("DM.BlogInvitation.GetUserPendingInvitations")
            .Where(t => !t.IsRemoved &&
                        t.UserId == userId &&
                        (t.Type == TokenType.BlogAssistantInvitation || t.Type == TokenType.BlogReaderInvitation))
            .Join(_dbContext.Blogs,
                t => t.EntityId,
                b => b.BlogId,
                (t, b) => new { Token = t, Blog = b })
            .Join(_dbContext.Users,
                tb => tb.Token.UserId,
                u => u.UserId,
                (tb, u) => new { tb.Token, tb.Blog, InvitedUser = u })
            .Select(tbu => new
            {
                tbu.Token.TokenId,
                tbu.Blog.BlogId,
                BlogTitle = tbu.Blog.Title,
                InvitedUserId = tbu.InvitedUser.UserId,
                InvitedUsername = tbu.InvitedUser.Username,
                InviterUserId = tbu.Token.CreatorId ?? tbu.Blog.AuthorId,
                InviterUsername = tbu.Token.Creator != null
                    ? tbu.Token.Creator.Username
                    : _dbContext.Users.Where(u => u.UserId == tbu.Blog.AuthorId).Select(u => u.Username).FirstOrDefault(),
                tbu.Token.Type,
                tbu.Token.CreatedUtc
            })
            .OrderByDescending(d => d.CreatedUtc)
            .ToArrayAsync(ct);

        return data.Select(d => new BlogInvitation
        {
            TokenId = d.TokenId,
            BlogId = d.BlogId,
            BlogTitle = d.BlogTitle,
            InvitedUser = new GeneralUser { UserId = d.InvitedUserId, Username = d.InvitedUsername },
            InvitedBy = new GeneralUser { UserId = d.InviterUserId, Username = d.InviterUsername ?? "" },
            TargetRole = d.Type == TokenType.BlogAssistantInvitation ? BlogRole.Assistant : BlogRole.Reader,
            CreatedUtc = d.CreatedUtc,
            ExpiresUtc = d.CreatedUtc.AddDays(InvitationPolicy.ExpirationDays)
        });
    }
}
