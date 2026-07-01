using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for community users (public profiles)
/// </summary>
internal class CommunityUserApiService : ICommunityUserApiService
{
    private readonly ICommunityProfileService _profileService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUserProfileNoteService _profileNoteService;
    private readonly ILoginRecordRepository _loginRecordRepository;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommunityUserApiService(
        ICommunityProfileService profileService,
        IUserLookupService userLookupService,
        IUserProfileNoteService profileNoteService,
        ILoginRecordRepository loginRecordRepository,
        IMapper mapper)
    {
        _profileService = profileService;
        _userLookupService = userLookupService;
        _profileNoteService = profileNoteService;
        _loginRecordRepository = loginRecordRepository;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetUsers(UsersQuery query)
    {
        // Parse sort order: default depends on sort field
        // - Name: ascending (alphabetical)
        // - Others: descending (best/newest first)
        var sortAscending = query.SortOrder?.ToLowerInvariant() switch
        {
            "asc" => true,
            "desc" => false,
            _ => query.Sort == UserSort.Name // Default: asc for Name, desc for others
        };

        var (users, paging) = await _profileService.GetUsers(
            query,
            query.Activity,
            query.Q,
            query.Role,
            query.Sort,
            sortAscending,
            query.IsHonorary,
            query.IsNewbie,
            query.IsOnline,
            query.MinRating,
            query.MaxRating,
            query.MinGamesHosting,
            query.MaxGamesHosting,
            query.MinGamesPlaying,
            query.MaxGamesPlaying,
            query.MinBlogsHosting,
            query.MaxBlogsHosting,
            query.RegisteredFromUtc,
            query.RegisteredToUtc);
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
        var user = await _userLookupService.GetAsync(username);
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

        // Username history is public — always fetch. Personal note is
        // per-viewer (each authenticated user keeps their own private note
        // about this subject) and the underlying service throws
        // UnauthorizedAccessException for anonymous callers. Profile pages
        // must remain accessible without login, so guard the note fetch.
        var usernameHistory = await _profileService.GetUsernameHistory(user.UserId);

        UserProfileNote? personalNote = null;
        try
        {
            personalNote = await _profileNoteService.GetNote(username);
        }
        catch (UnauthorizedAccessException)
        {
            // Anonymous viewer — no personal note to show. Continue.
        }

        var profile = _mapper.Map<UserProfile>(user);
        profile.UsernameHistory = usernameHistory.Select(_mapper.Map<UsernameHistoryEntry>).ToList();
        // GeneralUser→UserProfile mapping ignores Contacts and Info because
        // the base domain type lacks them; UserDetails (the actual source
        // returned here) carries both. Populate them after the base map.
        profile.Contacts = user.Contacts
            .OrderBy(c => c.SortOrder)
            .Select(_mapper.Map<Contact>)
            .ToList();
        profile.Info = string.IsNullOrEmpty(user.Info)
            ? null
            : new DM.Web.API.Shared.BbRendering.InfoBbText { Value = user.Info };

        if (personalNote != null)
        {
            profile.PersonalNote = new PersonalNote
            {
                Id = personalNote.Id,
                Text = personalNote.Text,
                CreatedUtc = personalNote.CreatedUtc,
                UpdatedUtc = personalNote.UpdatedUtc
            };
        }

        return new Envelope<UserProfile>(profile);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<LoginHistoryDto>> GetLoginHistory(string username)
    {
        var user = await _userLookupService.GetAsync(username);
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
