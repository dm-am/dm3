using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Blog.Features.Blacklists;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Blacklists;

/// <inheritdoc />
internal class BlogBlacklistApiService : IBlogBlacklistApiService
{
    private readonly IBlogBlacklistService _blacklistService;
    private readonly UserMapper _mapper;

    public BlogBlacklistApiService(
        IBlogBlacklistService blacklistService,
        UserMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> Get(Guid blogId)
    {
        var users = await _blacklistService.Get(blogId);
        return users.Select(_mapper.ToUser);
    }

    /// <inheritdoc />
    public async Task<User> Create(Guid blogId, string username)
    {
        var dto = new OperateBlogBlacklistLink { BlogId = blogId, Username = username };
        var user = await _blacklistService.Add(dto);
        return _mapper.ToUser(user);
    }

    /// <inheritdoc />
    public Task Delete(Guid blogId, string username) =>
        _blacklistService.Remove(new OperateBlogBlacklistLink { BlogId = blogId, Username = username });
}
