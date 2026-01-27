using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using Microsoft.AspNetCore.Http;
using UserDetails = DM.Web.API.Dto.Users.UserDetails;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class UserApiService : IUserApiService
{
    private readonly IUserReadingService readingService;
    private readonly IUserUpdatingService updatingService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public UserApiService(
        IUserReadingService readingService,
        IUserUpdatingService updatingService,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.updatingService = updatingService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsers(UsersQuery query)
    {
        var (users, paging) = await readingService.Get(query, query.Inactive, query.Search);
        return new ListEnvelope<User>(users.Select(mapper.Map<User>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> GetUser(string login)
    {
        var user = await readingService.Get(login);
        return new Envelope<User>(mapper.Map<User>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> GetUser(Guid userId)
    {
        var user = await readingService.Get(userId);
        return new Envelope<User>(mapper.Map<User>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> GetCurrentUser()
    {
        var user = await readingService.GetCurrent();
        return new Envelope<User>(mapper.Map<User>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> GetUserDetails(string login)
    {
        var user = await readingService.GetDetails(login);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> GetUserDetails(Guid userId)
    {
        var user = await readingService.GetDetails(userId);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> UpdateUser(string login, UserDetails user)
    {
        var updateUser = mapper.Map<UpdateUser>(user);
        updateUser.Login = login;
        var updatedUser = await updatingService.Update(updateUser);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(updatedUser));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> UploadProfilePicture(string login, IFormFile file)
    {
        await using var uploadStream = file.OpenReadStream();
        var updatedUser = await updatingService.UploadPicture(login, uploadStream, file.Name, file.ContentType);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(updatedUser));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsersByRole(UserRole role)
    {
        var users = await readingService.GetByRole(role);
        return new ListEnvelope<User>(users.Select(mapper.Map<User>));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserSettings>> GetUserSettings(string login)
    {
        var user = await readingService.GetDetails(login);
        return new Envelope<UserSettings>(mapper.Map<UserSettings>(user.Settings));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserSettings>> UpdateUserSettings(string login, UserSettings settings)
    {
        var updateUser = new UpdateUser
        {
            Login = login,
            Settings = mapper.Map<DM.Services.Authentication.Dto.UserSettings>(settings)
        };
        var updatedUser = await updatingService.Update(updateUser);
        return new Envelope<UserSettings>(mapper.Map<UserSettings>(updatedUser.Settings));
    }
}