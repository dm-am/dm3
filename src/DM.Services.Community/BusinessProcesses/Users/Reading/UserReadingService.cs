using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Caching;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <inheritdoc />
internal class UserReadingService : IUserReadingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserReadingRepository _readingRepository;
    private readonly ICache _cache;

    /// <inheritdoc />
    public UserReadingService(
        IIdentityProvider identityProvider,
        IUserReadingRepository readingRepository,
        ICache cache)
    {
        _identityProvider = identityProvider;
        _readingRepository = readingRepository;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<GeneralUser> users, PagingResult paging)> Get(
        PagingQuery query, bool withInactive, string search = null)
    {
        var totalCount = await _readingRepository.CountUsers(withInactive, search);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.EntitiesPerPage, totalCount);
        var users = await _readingRepository.GetUsers(paging, withInactive, search);
        return (users, paging.Result);
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Get(string login)
    {
        var user = await _readingRepository.GetUser(login);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {login} not found");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> Get(Guid userId)
    {
        var user = await _readingRepository.GetUser(userId);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {userId} not found");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> GetCurrent()
    {
        var identity = _identityProvider.Current;
        if (!identity.User.IsAuthenticated)
        {
            throw new HttpException(HttpStatusCode.Unauthorized, "User is not authenticated");
        }

        return await Get(identity.User.UserId);
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetails(string login)
    {
        var normalizedLogin = login.ToLowerInvariant();
        var user = await _cache.GetOrCreate(
            $"user_details_{normalizedLogin}",
            () => _readingRepository.GetUserDetails(login),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {login} not found");
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails> GetDetails(Guid userId)
    {
        var user = await _cache.GetOrCreate(
            $"user_details_{userId}",
            () => _readingRepository.GetUserDetails(userId),
            CachePolicy.Medium);

        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {userId} not found");
        }

        return user;
    }

    /// <inheritdoc />
    public Task<IEnumerable<GeneralUser>> GetByRole(UserRole role) =>
        _cache.GetOrCreate(
            $"users_by_role_{role}",
            () => _readingRepository.GetUsersByRole(role),
            CachePolicy.LongLived);
}