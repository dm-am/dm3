using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for community users (public profiles)
/// </summary>
internal class CommunityUserApiService : ICommunityUserApiService
{
    private readonly ICommunityProfileService _profileService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUserProfileNoteService _profileNoteService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public CommunityUserApiService(
        ICommunityProfileService profileService,
        IUserLookupService userLookupService,
        IUserProfileNoteService profileNoteService,
        ILoginRecordService loginRecordService,
        IMapper mapper)
    {
        _profileService = profileService;
        _userLookupService = userLookupService;
        _profileNoteService = profileNoteService;
        _loginRecordService = loginRecordService;
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

        // Mapped by name, so a filter added to the query string and the domain
        // record needs no edit here. The direction is the one thing this layer
        // decides, above.
        var filter = _mapper.Map<UserFilter>(query) with { SortAscending = sortAscending };

        var (users, paging) = await _profileService.GetUsers(query, filter);
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
        // Owner of the profile bio is the profile subject. Populate the
        // render-context envelope so the JSON converter honors the owner's
        // AuthorEdit round-trip and downgrades any other viewer's author_edit
        // request to permission-filtered Display. Profile surface allows
        // neither [mod] nor [private], so this is round-trip integrity rather
        // than a leak vector, but the owner id is set for correctness.
        profile.Info = string.IsNullOrEmpty(user.Info)
            ? null
            : new InfoBbText
            {
                Value = user.Info,
                Context = new RenderContextEnvelope
                {
                    Surface = BbSurface.Profile,
                    PostAuthorUserId = user.UserId
                }
            };

        if (personalNote != null)
        {
            profile.PersonalNote = new PersonalNote
            {
                Id = personalNote.Id,
                Text = personalNote.Text,
                CreatedUtc = personalNote.CreatedUtc,
                ModifiedUtc = personalNote.ModifiedUtc
            };
        }

        return new Envelope<UserProfile>(profile);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<LoginHistoryDto>> GetLoginHistory(string username, PagingQuery query)
    {
        var user = await _userLookupService.GetAsync(username);
        var loginHistory = await _loginRecordService.GetHistory(user.UserId, query);
        var total = await _loginRecordService.CountHistory(user.UserId);
        var dtos = loginHistory.Select(r => new LoginHistoryDto
        {
            LoginTimestampUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent
        });
        return new ListEnvelope<LoginHistoryDto>(dtos, new PagingInfo(query.Skip, query.Take, total));
    }
}
