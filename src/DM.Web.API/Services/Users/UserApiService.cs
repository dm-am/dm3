using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Game.BusinessProcesses.Posts.Reading;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using UserDetails = DM.Web.API.Dto.Users.UserDetails;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class UserApiService : IUserApiService
{
    private readonly IUserReadingService readingService;
    private readonly IUserUpdatingService updatingService;
    private readonly IPostReadingService postReadingService;
    private readonly IIdentityProvider identityProvider;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public UserApiService(
        IUserReadingService readingService,
        IUserUpdatingService updatingService,
        IPostReadingService postReadingService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        this.readingService = readingService;
        this.updatingService = updatingService;
        this.postReadingService = postReadingService;
        this.identityProvider = identityProvider;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsers(UsersQuery query)
    {
        var (users, paging) = await readingService.Get(query, query.Filter, query.Search);
        return new ListEnvelope<User>(users.Select(mapper.Map<User>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> GetUser(string login)
    {
        var user = await readingService.Get(login);
        return new Envelope<User>(mapper.Map<User>(user));
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> GetUserDetails(string login)
    {
        var user = await readingService.GetDetails(login);
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

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> UpdateCurrentUserProfile(UpdateProfile profile)
    {
        var currentUser = identityProvider.Current.User;
        var updateUser = mapper.Map<UpdateUser>(profile);
        updateUser.Login = currentUser.Login;

        var updatedUser = await updatingService.Update(updateUser);
        return new Envelope<UserDetails>(mapper.Map<UserDetails>(updatedUser));
    }

    /// <inheritdoc />
    public async Task<Envelope<BestPost>> GetBestPost(string login)
    {
        var user = await readingService.Get(login);
        var bestPost = await postReadingService.GetBestPost(user.UserId);

        if (bestPost == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "No best post found for this user");
        }

        return new Envelope<BestPost>(mapper.Map<BestPost>(bestPost));
    }
}