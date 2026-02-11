using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Blogs.Invitations;

/// <inheritdoc />
internal class BlogInvitationRepository : IBlogInvitationRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public BlogInvitationRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Guid>> FindInvitations(Guid blogId, Guid userId, TokenType type) =>
        await _dbContext.Tokens
            .Where(t => !t.IsRemoved &&
                        t.EntityId == blogId &&
                        t.UserId == userId &&
                        t.Type == type)
            .Select(t => t.TokenId)
            .ToArrayAsync();

    /// <inheritdoc />
    public async Task InvalidateAndCreate(IEnumerable<IUpdateBuilder<Token>> updates, Token token)
    {
        foreach (var update in updates)
        {
            update.AttachTo(_dbContext);
        }

        _dbContext.Tokens.Add(token);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task Update(IUpdateBuilder<Token> update)
    {
        update.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<Guid?> FindBlogByToken(Guid tokenId, Guid userId, TokenType type)
    {
        var token = await _dbContext.Tokens
            .Where(t => !t.IsRemoved &&
                        t.TokenId == tokenId &&
                        t.UserId == userId &&
                        t.Type == type)
            .Select(t => t.EntityId)
            .FirstOrDefaultAsync();

        return token;
    }

    /// <inheritdoc />
    public async Task<BlogInvitationInfo?> GetInvitation(Guid tokenId)
    {
        return await _dbContext.Tokens
            .Where(t => !t.IsRemoved && t.TokenId == tokenId)
            .Join(_dbContext.Blogs,
                t => t.EntityId,
                b => b.BlogId,
                (t, b) => new { Token = t, Blog = b })
            .Join(_dbContext.Users,
                tb => tb.Token.UserId,
                u => u.UserId,
                (tb, u) => new BlogInvitationInfo
                {
                    TokenId = tb.Token.TokenId,
                    BlogId = tb.Blog.BlogId,
                    BlogTitle = tb.Blog.Title,
                    UserId = u.UserId,
                    UserLogin = u.Login,
                    Type = tb.Token.Type,
                    CreatedUtc = tb.Token.CreatedUtc
                })
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitationInfo>> GetPendingInvitations(Guid blogId)
    {
        return await _dbContext.Tokens
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
                (tb, u) => new BlogInvitationInfo
                {
                    TokenId = tb.Token.TokenId,
                    BlogId = tb.Blog.BlogId,
                    BlogTitle = tb.Blog.Title,
                    UserId = u.UserId,
                    UserLogin = u.Login,
                    Type = tb.Token.Type,
                    CreatedUtc = tb.Token.CreatedUtc
                })
            .OrderByDescending(i => i.CreatedUtc)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BlogInvitationInfo>> GetUserPendingInvitations(Guid userId)
    {
        return await _dbContext.Tokens
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
                (tb, u) => new BlogInvitationInfo
                {
                    TokenId = tb.Token.TokenId,
                    BlogId = tb.Blog.BlogId,
                    BlogTitle = tb.Blog.Title,
                    UserId = u.UserId,
                    UserLogin = u.Login,
                    Type = tb.Token.Type,
                    CreatedUtc = tb.Token.CreatedUtc
                })
            .OrderByDescending(i => i.CreatedUtc)
            .ToArrayAsync();
    }
}
