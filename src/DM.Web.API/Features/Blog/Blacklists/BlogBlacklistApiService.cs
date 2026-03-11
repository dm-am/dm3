using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Blog.Features.Blacklists;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Blog.Blacklists;

/// <inheritdoc />
internal class BlogBlacklistApiService : IBlogBlacklistApiService
{
    private readonly IBlogBlacklistService _blacklistService;
    private readonly IMapper _mapper;

    public BlogBlacklistApiService(
        IBlogBlacklistService blacklistService,
        IMapper mapper)
    {
        _blacklistService = blacklistService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<User>> Get(Guid blogId)
    {
        var users = await _blacklistService.Get(blogId);
        return users.Select(_mapper.Map<User>);
    }

    /// <inheritdoc />
    public async Task<User> Create(Guid blogId, string username)
    {
        var user = await _blacklistService.Add(blogId, username);
        return _mapper.Map<User>(user);
    }

    /// <inheritdoc />
    public Task Delete(Guid blogId, string username) =>
        _blacklistService.Remove(blogId, username);
}
