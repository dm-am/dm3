using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Shared.Dto;
using IPostService = DM.Domain.Game.Features.Posts.IPostService;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for community users (public profiles)
/// </summary>
internal class CommunityUserApiService : ICommunityUserApiService
{
    private readonly ICommunityProfileService _profileService;
    private readonly IUserLookupService _userLookupService;
    private readonly IPostService _postService;
    private readonly IUserProfileNoteService _profileNoteService;
    private readonly ILoginRecordRepository _loginRecordRepository;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommunityUserApiService(
        ICommunityProfileService profileService,
        IUserLookupService userLookupService,
        IPostService postService,
        IUserProfileNoteService profileNoteService,
        ILoginRecordRepository loginRecordRepository,
        IMapper mapper)
    {
        _profileService = profileService;
        _userLookupService = userLookupService;
        _postService = postService;
        _profileNoteService = profileNoteService;
        _loginRecordRepository = loginRecordRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsers(UsersQuery query)
    {
        var (users, paging) = await _profileService.GetUsers(query, query.Activity, query.Q, query.Role, query.Sort);
        return new ListEnvelope<User>(users.Select(_mapper.Map<User>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsersByRole(UserRole role)
    {
        var users = await _profileService.GetUsersByRole(role);
        return new ListEnvelope<User>(users.Select(_mapper.Map<User>));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> GetUser(string username)
    {
        var user = await _userLookupService.Get(username);
        var userDto = _mapper.Map<User>(user);

        // Populate username history
        var usernameHistory = await _profileService.GetUsernameHistory(user.UserId);
        userDto.UsernameHistory = usernameHistory.Select(_mapper.Map<UsernameHistoryEntry>).ToList();

        return new Envelope<User>(userDto);
    }

    /// <inheritdoc />
    public async Task<Envelope<UserProfile>> GetUserProfile(string username)
    {
        var user = await _profileService.GetProfile(username);

        // Parallel queries
        var fetchUsernameHistory = _profileService.GetUsernameHistory(user.UserId);
        var fetchBestPost = _postService.GetBestPostAsync(user.UserId);
        var fetchPersonalNote = _profileNoteService.GetNote(username);

        await Task.WhenAll(fetchUsernameHistory, fetchBestPost, fetchPersonalNote);

        var profile = _mapper.Map<UserProfile>(user);
        profile.UsernameHistory = fetchUsernameHistory.Result.Select(_mapper.Map<UsernameHistoryEntry>).ToList();

        var bestPost = fetchBestPost.Result;
        if (bestPost != null)
        {
            profile.FeaturedPost = _mapper.Map<FeaturedPost>(bestPost);
        }

        var personalNote = fetchPersonalNote.Result;
        if (personalNote != null)
        {
            profile.PersonalNote = new PersonalNote
            {
                Id = personalNote.NoteId,
                Text = personalNote.Text,
                CreatedUtc = personalNote.CreatedUtc,
                UpdatedUtc = personalNote.UpdatedUtc
            };
        }

        return new Envelope<UserProfile>(profile);
    }

    /// <inheritdoc />
    public async Task<Envelope<FeaturedPost>> GetFeaturedPost(string username)
    {
        var user = await _userLookupService.Get(username);
        var bestPost = await _postService.GetBestPostAsync(user.UserId);
        return new Envelope<FeaturedPost>(bestPost != null ? _mapper.Map<FeaturedPost>(bestPost) : null!);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<LoginHistoryDto>> GetLoginHistory(string username)
    {
        var user = await _userLookupService.Get(username);
        var loginHistory = await _loginRecordRepository.GetLoginHistory(user.UserId);
        var dtos = loginHistory.Select(r => new LoginHistoryDto
        {
            LoginTimestampUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent
        });
        return new ListEnvelope<LoginHistoryDto>(dtos);
    }
}
