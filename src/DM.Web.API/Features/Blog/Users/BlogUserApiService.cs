using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Users;

/// <inheritdoc />
internal class BlogUserApiService : IBlogUserApiService
{
    private readonly IBlogService _blogService;
    private readonly IMapper _mapper;

    public BlogUserApiService(
        IBlogService blogService,
        IMapper mapper)
    {
        _blogService = blogService;
        _mapper = mapper;
    }

    #region Users

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetUsers(Guid blogId, string? role = null)
    {
        var users = new List<BlogUser>();

        // Get owner from blog
        var blog = await _blogService.Get(blogId);
        var owner = new BlogUser
        {
            User = _mapper.Map<User>(blog.Author),
            Role = nameof(BlogRole.Owner),
            JoinedUtc = blog.CreatedAt
        };

        // Get mentor if assigned
        BlogUser? mentor = null;
        if (blog.Mentor != null)
        {
            mentor = new BlogUser
            {
                User = _mapper.Map<User>(blog.Mentor),
                Role = nameof(BlogRole.Mentor),
                JoinedUtc = null // Mentor assignment date not tracked in model
            };
        }

        // Get assistants
        var assistants = await _blogService.GetAssistants(blogId);
        var assistantDtos = assistants.Select(a => new BlogUser
        {
            User = _mapper.Map<User>(a.User),
            Role = nameof(BlogRole.Assistant),
            JoinedUtc = a.JoinedUtc
        }).ToList();

        // Get readers
        var readers = await _blogService.GetReaders(blogId);
        var readerDtos = readers.Select(r => new BlogUser
        {
            User = _mapper.Map<User>(r),
            Role = nameof(BlogRole.Reader),
            JoinedUtc = null // Subscription date not included in GeneralUser
        }).ToList();

        // Apply role filter or return all
        if (string.IsNullOrEmpty(role))
        {
            users.Add(owner);
            if (mentor != null) users.Add(mentor);
            users.AddRange(assistantDtos);
            users.AddRange(readerDtos);
        }
        else
        {
            switch (role.ToLowerInvariant())
            {
                case "owner":
                    users.Add(owner);
                    break;
                case "mentor":
                    if (mentor != null) users.Add(mentor);
                    break;
                case "assistant":
                    users.AddRange(assistantDtos);
                    break;
                case "reader":
                    users.AddRange(readerDtos);
                    break;
                default:
                    // Unknown role - return all
                    users.Add(owner);
                    if (mentor != null) users.Add(mentor);
                    users.AddRange(assistantDtos);
                    users.AddRange(readerDtos);
                    break;
            }
        }

        return users;
    }

    /// <inheritdoc />
    public async Task RemoveUser(Guid blogId, Guid userId)
    {
        // Only assistants can be removed - readers can only unsubscribe themselves
        // Use blacklist for problematic readers
        var assistants = await _blogService.GetAssistants(blogId);
        var assistant = assistants.FirstOrDefault(a => a.User.UserId == userId);
        if (assistant != null)
        {
            await _blogService.RemoveAssistant(blogId, assistant.User.Username);
        }
    }

    #endregion

    #region Readers

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetReaders(Guid blogId)
    {
        var readers = await _blogService.GetReaders(blogId);
        return readers.Select(r => new BlogUser
        {
            User = _mapper.Map<User>(r),
            Role = nameof(BlogRole.Reader),
            JoinedUtc = null // Subscription date not included in GeneralUser
        });
    }

    /// <inheritdoc />
    public async Task<BlogUser> Subscribe(Guid blogId)
    {
        var user = await _blogService.Subscribe(blogId);
        return new BlogUser
        {
            User = _mapper.Map<User>(user),
            Role = nameof(BlogRole.Reader),
            JoinedUtc = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task Unsubscribe(Guid blogId)
    {
        await _blogService.Unsubscribe(blogId);
    }

    // Note: RemoveReader is not provided - readers can only unsubscribe themselves

    #endregion

    #region Assistants

    /// <inheritdoc />
    public async Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId)
    {
        var assistants = await _blogService.GetAssistants(blogId);
        return assistants.Select(a => new BlogUser
        {
            User = _mapper.Map<User>(a.User),
            Role = nameof(BlogRole.Assistant),
            JoinedUtc = a.JoinedUtc
        });
    }

    /// <inheritdoc />
    public async Task RemoveAssistantByUsername(Guid blogId, string username)
    {
        await _blogService.RemoveAssistant(blogId, username);
    }

    #endregion
}
