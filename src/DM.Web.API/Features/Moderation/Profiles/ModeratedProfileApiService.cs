using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Profiles;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using ApiUserIpInfo = DM.Web.API.Features.Moderation.Profiles.UserIpInfo;
using ApiLinkedProfile = DM.Web.API.Features.Moderation.Profiles.LinkedProfile;
using ServiceUserIpInfo = DM.Domain.Account.Features.Authentication.UserIpInfo;
using ServiceLinkedProfile = DM.Domain.Account.Features.Authentication.LinkedProfile;
using ServiceUserLoginRecord = DM.Domain.Account.Features.Authentication.UserLoginRecord;
using ServiceRemovedAccount = DM.Domain.Account.Features.Deactivation.RemovedAccount;
using ServiceRemovedAccountsQuery = DM.Domain.Account.Features.Deactivation.RemovedAccountsQuery;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// API service for moderated user profiles
/// </summary>
internal class ModeratedProfileApiService : IModeratedProfileApiService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IModeratedProfileService _moderatedProfileService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly DM.Web.API.Features.Moderation.Warnings.IWarningApiService _warningApiService;
    private readonly DM.Web.API.Features.Moderation.Bans.IBanApiService _banApiService;
    private readonly IModeratedProfileNoteApiService _moderatorNoteApiService;
    private readonly IUserProfileNoteService _personalNoteService;
    private readonly IDeactivationService _deactivationService;
    private readonly UserMapper _mapper;

    /// <inheritdoc />
    public ModeratedProfileApiService(
        IIdentityProvider identityProvider,
        IModeratedProfileService moderatedProfileService,
        ILoginRecordService loginRecordService,
        DM.Web.API.Features.Moderation.Warnings.IWarningApiService warningApiService,
        DM.Web.API.Features.Moderation.Bans.IBanApiService banApiService,
        IModeratedProfileNoteApiService moderatorNoteApiService,
        IUserProfileNoteService personalNoteService,
        IDeactivationService deactivationService,
        UserMapper mapper)
    {
        _identityProvider = identityProvider;
        _moderatedProfileService = moderatedProfileService;
        _loginRecordService = loginRecordService;
        _warningApiService = warningApiService;
        _banApiService = banApiService;
        _moderatorNoteApiService = moderatorNoteApiService;
        _personalNoteService = personalNoteService;
        _deactivationService = deactivationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ModeratedProfile> GetModeratedProfile(string username)
    {
        var caller = _identityProvider.Current.User;
        var callerRole = caller.Role;
        var isAdmin = callerRole >= UserRole.Admin;
        var isSeniorMod = callerRole >= UserRole.SeniorModerator;

        // Get target user
        var user = await _moderatedProfileService.GetProfile(username);

        // Sequential on purpose: every query below runs on this request's
        // single DbContext, and EF forbids concurrent operations on one
        // context. Admin-only queries are skipped for lower roles.
        var linkedProfiles = await _loginRecordService.GetLinkedProfiles(user.UserId);
        var moderatorNotes = await _moderatorNoteApiService.GetNotes(username);
        var personalNote = await _personalNoteService.GetNote(username);
        var warnings = await _warningApiService.GetUserWarnings(username);
        var banStatus = await _banApiService.GetUserBanStatus(username);
        var ipAddresses = isAdmin
            ? MapIpAddresses(await _loginRecordService.GetIpAddresses(user.UserId))
            : null;
        var loginHistory = isAdmin
            // The moderation profile shows the most recent logins, not the whole
            // journal: the page bound is the same one every other list obeys.
            ? MapLoginHistory(await _loginRecordService.GetHistory(user.UserId, new PagingQuery { Take = 50 }))
            : null;

        // Map the base UserProfile fields (Mapperly), then add the
        // moderation-specific facts. Info and Contacts stay unmapped on
        // purpose: the moderation panel renders inside the regular profile
        // page, whose community endpoint already carries the bio and the
        // contacts (CommunityUserApiService fills both, envelope included),
        // and the panel itself reads only the moderation members - a copy
        // here would be a second, unread answer to the same question.
        var profile = _mapper.ToModeratedProfile(user);

        // Admin-only fields (null for non-admin callers)
        profile.Email = isAdmin ? user.Email : null;
        profile.IpAddresses = ipAddresses;
        profile.LoginHistory = loginHistory;

        // Moderator+ fields
        profile.LinkedProfiles = MapLinkedProfiles(linkedProfiles);
        profile.ModeratorNotes = MapModeratorNotes(moderatorNotes, caller.UserId, isSeniorMod);

        // Personal note (caller's own note about this user)
        if (personalNote != null)
        {
            profile.PersonalNote = _mapper.ToPersonalNote(personalNote);
        }

        profile.Violations = new ViolationSummary
        {
            TotalWarnings = warnings.ActiveCount,
            ActiveWarningPoints = warnings.TotalPoints,
            TotalBans = banStatus.History?.Count() ?? 0,
            IsCurrentlyBanned = banStatus.IsBanned,
            CurrentBanEndUtc = banStatus.ActiveBan?.ExpiresUtc,
            // The reason as it was written: the panel prints it as text.
            CurrentBanReason = banStatus.ActiveBan?.Comment.Value
        };

        // A warning and a ban reach only a rank below the caller's, and never an
        // administrator (ModerationRank): the buttons are offered by the formula
        // the two services refuse by, so neither is drawn on a profile where the
        // write would come back forbidden.
        var reachesByRank = ModerationRank.Reaches(callerRole, user.Role);

        profile.Permissions = new ModerationPermissions
        {
            CanViewEmail = isAdmin,
            CanViewIpAddresses = isAdmin,
            CanViewLoginHistory = isAdmin,
            CanViewLinkedProfiles = true, // Controller already requires Moderator+
            CanViewModNotes = true,
            CanCreateModNote = true,
            CanIssueWarning = reachesByRank,
            CanIssueBan = isSeniorMod && reachesByRank,
            CanLiftBan = isSeniorMod,
            CanSetModerationWatch = true // Controller already requires Moderator+
        };

        return profile;
    }

    /// <inheritdoc />
    public async Task<UserProfile> ModerateUserProfile(string username, ModerateProfile profile)
    {
        var updatedUser = await _moderatedProfileService.ModerateProfile(username, profile.Info ?? string.Empty);
        return _mapper.ToUserProfile(updatedUser);
    }

    /// <inheritdoc />
    public async Task<Envelope<ModeratedProfile>> SetModerationWatch(string username, bool underWatch)
    {
        await _moderatedProfileService.SetModerationWatch(username, underWatch);
        // Re-read through the aggregate so the answer carries the same payload the
        // page was rendered from, permissions included, instead of a bare profile
        // the caller would have to merge by hand.
        //
        // Enveloped, unlike the two bare-bodied endpoints beside it: those are on
        // the inherited legacy list, which only ever shrinks, and a new endpoint
        // added to it would be new debt written down as history.
        return new Envelope<ModeratedProfile>(await GetModeratedProfile(username));
    }

    /// <inheritdoc />
    public async Task<UserProfile> SetUserRole(string username, UserRole role)
    {
        await _moderatedProfileService.SetUserRole(username, role);
        var user = await _moderatedProfileService.GetProfile(username);
        return _mapper.ToUserProfile(user);
    }

    /// <inheritdoc />
    public Task RemoveAccount(string username, RemoveAccountRequest request) =>
        _deactivationService.DeactivateByModeration(username, request.Reason);

    /// <inheritdoc />
    public Task RestoreAccount(string username) =>
        _deactivationService.RestoreByModeration(username);

    /// <inheritdoc />
    public async Task<ListEnvelope<RemovedAccount>> GetRemovedAccounts(RemovedAccountsQuery query)
    {
        var (accounts, paging) = await _deactivationService.GetRemovedAccounts(new ServiceRemovedAccountsQuery
        {
            Search = query.Search,
            Skip = query.Skip,
            Take = query.Take
        });

        return new ListEnvelope<RemovedAccount>(accounts.Select(Map).ToList(), new PagingInfo(paging));
    }

    /// <summary>
    /// A removed account on the wire.
    /// </summary>
    /// <remarks>
    /// The address is carried across exactly as the service handed it over and is
    /// never filled from anywhere else: below an administrator it arrives empty,
    /// and reading it off another source here would hand every address on the page
    /// to a moderator the moderated profile shows none to.
    /// </remarks>
    private static RemovedAccount Map(ServiceRemovedAccount account) => new()
    {
        Id = account.UserId,
        Username = account.Username,
        Email = account.Email,
        Role = account.Role,
        RegisteredUtc = account.RegisteredUtc,
        LastActivityUtc = account.LastActivityUtc
    };

    private static IReadOnlyList<ApiUserIpInfo> MapIpAddresses(IReadOnlyList<ServiceUserIpInfo> ips)
    {
        if (ips == null) return [];
        return ips.Select(ip => new ApiUserIpInfo
        {
            IpAddress = ip.IpAddress,
            FirstSeenUtc = ip.FirstSeenUtc,
            LastSeenUtc = ip.LastSeenUtc,
            LoginsCount = ip.LoginsCount
        }).ToList();
    }

    private static IReadOnlyList<LoginRecord> MapLoginHistory(IReadOnlyList<ServiceUserLoginRecord> records)
    {
        if (records == null) return [];
        return records.Select(r => new LoginRecord
        {
            LoginUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
            IsSuccessful = r.IsSuccessful
        }).ToList();
    }

    private static IReadOnlyList<ApiLinkedProfile> MapLinkedProfiles(IReadOnlyList<ServiceLinkedProfile> profiles)
    {
        return profiles.Select(p => new ApiLinkedProfile
        {
            UserId = p.UserId,
            Username = p.Username,
            SharedIpsCount = p.SharedIpsCount,
            LastSharedLoginUtc = p.LastSharedLoginUtc
        }).ToList();
    }

    private static IReadOnlyList<ModNote> MapModeratorNotes(
        IEnumerable<ModeratedProfileNote> notes,
        Guid callerId,
        bool isSeniorMod)
    {
        return notes.Select(n => new ModNote
        {
            Id = n.Id,
            AuthorUsername = n.Author?.Username ?? string.Empty,
            AuthorId = n.Author?.Id ?? Guid.Empty,
            Text = n.Text,
            CreatedUtc = n.CreatedUtc,
            ModifiedUtc = n.ModifiedUtc,
            CanEdit = isSeniorMod || (n.Author?.Id == callerId),
            CanDelete = isSeniorMod || (n.Author?.Id == callerId)
        }).ToList();
    }
}
