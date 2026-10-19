using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Blog.Users;

public class BlogUserApiService
{
    private readonly IBlogService _blogService;
    private readonly IBlogUserService _blogUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlogUserApiService(
        IBlogService blogService,
        IBlogUserService blogUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _blogService = blogService;
        _blogUserService = blogUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IEnumerable<BlogUser>> GetUsers(Guid blogId, BlogRole? role = null)
    {
        // A removed owner is nested as nobody, and a roster lists no row without a person.
        var blog = await _blogService.GetAsync(blogId);
        var author = blog.Author is null
            ? null
            : new BlogUser
            {
                User = UserRefMappers.ToUserRef(blog.Author),
                Role = BlogRole.Author,
                JoinedUtc = blog.CreatedUtc
            };

        var mentor = blog.Mentor is null
            ? null
            : new BlogUser
            {
                User = UserRefMappers.ToUserRef(blog.Mentor),
                Role = BlogRole.Mentor,
                JoinedUtc = null // Mentor assignment date not tracked in model
            };

        var assistants = await GetAssistants(blogId);
        var readers = await GetReaders(blogId);

        var users = new List<BlogUser>();
        if (role == null)
        {
            if (author != null) users.Add(author);
            if (mentor != null) users.Add(mentor);
            users.AddRange(assistants);
            users.AddRange(readers);
        }
        else
        {
            // No default arm: model binding refuses a value outside the vocabulary,
            // and a filter that silently widens reads as the filtered answer.
            switch (role.Value)
            {
                case BlogRole.Author:
                    if (author != null) users.Add(author);
                    break;
                case BlogRole.Mentor:
                    if (mentor != null) users.Add(mentor);
                    break;
                case BlogRole.Assistant:
                    users.AddRange(assistants);
                    break;
                case BlogRole.Reader:
                    users.AddRange(readers);
                    break;
            }
        }

        return users;
    }

    public async Task<IEnumerable<BlogUser>> GetReaders(Guid blogId)
    {
        var readers = await _blogUserService.GetReaders(blogId);
        return readers.Select(r => new BlogUser
        {
            User = UserRefMappers.ToUserRef(r),
            Role = BlogRole.Reader,
            JoinedUtc = null // Subscription date not included in GeneralUser
        });
    }

    public async Task<BlogUser> Subscribe(Guid blogId)
    {
        var user = await _blogUserService.Subscribe(blogId);
        return new BlogUser
        {
            User = UserRefMappers.ToUserRef(user),
            Role = BlogRole.Reader,
            JoinedUtc = _dateTimeProvider.Now
        };
    }

    public Task Unsubscribe(Guid blogId) => _blogUserService.Unsubscribe(blogId);

    public async Task<IEnumerable<BlogUser>> GetAssistants(Guid blogId)
    {
        var assistants = await _blogUserService.GetAssistants(blogId);
        return assistants.Select(a => new BlogUser
        {
            User = UserRefMappers.ToUserRef(a.User),
            Role = BlogRole.Assistant,
            JoinedUtc = a.JoinedUtc
        });
    }

    public Task RemoveAssistantByUsername(Guid blogId, string username) =>
        _blogUserService.RemoveAssistant(blogId, username);
}
