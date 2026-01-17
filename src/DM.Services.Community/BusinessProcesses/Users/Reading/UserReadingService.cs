using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <inheritdoc />
internal class UserReadingService : IUserReadingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserReadingRepository _readingRepository;

    /// <inheritdoc />
    public UserReadingService(
        IIdentityProvider identityProvider,
        IUserReadingRepository readingRepository)
    {
        _identityProvider = identityProvider;
        _readingRepository = readingRepository;
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
    public async Task<UserDetails> GetDetails(string login)
    {
        var user = await _readingRepository.GetUserDetails(login);
        if (user == null)
        {
            throw new HttpException(HttpStatusCode.Gone, $"User {login} not found");
        }

        return user;
    }
}