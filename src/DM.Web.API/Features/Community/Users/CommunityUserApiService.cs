using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Community.Features.Profiles;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// API service for community users (public profiles)
/// </summary>
public class CommunityUserApiService
{
    private readonly ICommunityProfileService _profileService;
    private readonly IUserLookupService _userLookupService;
    private readonly IUserProfileNoteService _profileNoteService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMessageService _messageService;
    private readonly IIdentityProvider _identityProvider;
    private readonly UserMapper _mapper;

    public CommunityUserApiService(
        ICommunityProfileService profileService,
        IUserLookupService userLookupService,
        IUserProfileNoteService profileNoteService,
        ILoginRecordService loginRecordService,
        IUserBlacklistChecker blacklistChecker,
        IMessageService messageService,
        IIdentityProvider identityProvider,
        UserMapper mapper)
    {
        _profileService = profileService;
        _userLookupService = userLookupService;
        _profileNoteService = profileNoteService;
        _loginRecordService = loginRecordService;
        _blacklistChecker = blacklistChecker;
        _messageService = messageService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<User>> GetUsers(UsersQuery query)
    {
        // The default direction depends on the field: ascending by name, descending otherwise.
        var sortAscending = query.SortOrder?.ToLowerInvariant() switch
        {
            "asc" => true,
            "desc" => false,
            _ => query.SortBy == UserSort.Name
        };

        var filter = _mapper.ToUserFilter(query) with { SortAscending = sortAscending };

        var (users, paging) = await _profileService.GetUsers(query, filter);
        return new ListEnvelope<User>(users.Select(_mapper.ToUser), new PagingInfo(paging));
    }

    public async Task<Envelope<User>> GetUser(string username)
    {
        var user = await _userLookupService.GetAsync(username);
        var userDto = _mapper.ToUser(user);

        var usernameHistory = await _profileService.GetUsernameHistory(user.UserId);
        userDto.UsernameHistory = usernameHistory.Select(_mapper.ToUsernameHistoryEntry).ToList();

        return new Envelope<User>(userDto);
    }

    public async Task<Envelope<UserProfile>> GetUserProfile(string username)
    {
        // "Аккаунт удален" is no account: its profile is the sum of every removed
        // one, asked before a lookup that would answer 404.
        if (RemovedAccount.Is(username))
        {
            var summary = await _profileService.GetRemovedAccountSummary();
            return new Envelope<UserProfile>(_mapper.ToRemovedAccountProfile(summary));
        }

        var user = await _profileService.GetProfile(username);

        var usernameHistory = await _profileService.GetUsernameHistory(user.UserId);

        // The viewer's own note about this user; null for an anonymous viewer.
        var personalNote = await _profileNoteService.GetNote(username);

        var profile = _mapper.ToUserProfile(user);
        profile.UsernameHistory = usernameHistory.Select(_mapper.ToUsernameHistoryEntry).ToList();
        // GeneralUser lacks Contacts and Info; UserDetails, the source here, has both.
        profile.Contacts = user.Contacts
            .OrderBy(c => c.SortOrder)
            .Select(_mapper.ToContact)
            .ToList();
        profile.Info = string.IsNullOrEmpty(user.Info) ? null : new InfoBbText { Value = user.Info };
        profile.Info?.BindAuthor(user.UserId);

        if (personalNote != null)
        {
            profile.PersonalNote = _mapper.ToPersonalNote(personalNote);
        }

        // The viewer's own list, and only that direction (see BlockedByViewer).
        if (AnonymousIdentity.Of(_identityProvider.Current?.User) is { } viewerId && viewerId != user.UserId)
        {
            profile.BlockedByViewer = await _blacklistChecker.IsBlockedAsync(viewerId, user.UserId);
        }

        // Asked of MessageService, which takes or refuses the first message by the same rule.
        profile.Viewer = new ProfileViewer
        {
            CanMessage = await _messageService.MayWriteToAsync(user.UserId)
        };

        return new Envelope<UserProfile>(profile);
    }

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
