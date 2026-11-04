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
using DM.Web.API.Features.Moderation.Bans;
using DM.Web.API.Features.Moderation.Warnings;
using DM.Web.API.Shared.Dto;
using ApiUserIpInfo = DM.Web.API.Features.Moderation.Profiles.UserIpInfo;
using ApiLinkedProfile = DM.Web.API.Features.Moderation.Profiles.LinkedProfile;
using ServiceUserIpInfo = DM.Domain.Account.Features.Authentication.UserIpInfo;
using ServiceLinkedProfile = DM.Domain.Account.Features.Authentication.LinkedProfile;
using ServiceUserLoginRecord = DM.Domain.Account.Features.Authentication.UserLoginRecord;
using ServiceRemovedAccount = DM.Domain.Account.Features.Deactivation.RemovedAccountRow;
using ServiceRemovedAccountsQuery = DM.Domain.Account.Features.Deactivation.RemovedAccountsQuery;
using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// API service for moderated user profiles
/// </summary>
public class ModeratedProfileApiService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IModeratedProfileService _moderatedProfileService;
    private readonly ILoginRecordService _loginRecordService;
    private readonly WarningApiService _warningApiService;
    private readonly BanApiService _banApiService;
    private readonly ModeratedProfileNoteApiService _moderatorNoteApiService;
    private readonly IUserProfileNoteService _personalNoteService;
    private readonly IDeactivationService _deactivationService;
    private readonly UserMapper _mapper;

    public ModeratedProfileApiService(
        IIdentityProvider identityProvider,
        IModeratedProfileService moderatedProfileService,
        ILoginRecordService loginRecordService,
        WarningApiService warningApiService,
        BanApiService banApiService,
        ModeratedProfileNoteApiService moderatorNoteApiService,
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

    /// <summary>The fields are filtered by the caller's role.</summary>
    public async Task<ModeratedProfile> GetModeratedProfile(string username)
    {
        var caller = _identityProvider.Current.User;
        var callerRole = caller.Role;
        var isAdmin = callerRole >= UserRole.Admin;
        var isSeniorMod = callerRole >= UserRole.SeniorModerator;

        var user = await _moderatedProfileService.GetProfile(username);

        // Sequential: the queries share the request's DbContext.
        var linkedProfiles = await _loginRecordService.GetLinkedProfiles(user.UserId);
        var moderatorNotes = await _moderatorNoteApiService.GetNotes(username);
        var personalNote = await _personalNoteService.GetNote(username);
        var warnings = await _warningApiService.GetUserWarnings(username);
        var banStatus = await _banApiService.GetUserBanStatus(username);
        var ipAddresses = isAdmin
            ? MapIpAddresses(await _loginRecordService.GetIpAddresses(user.UserId))
            : null;
        var loginHistory = isAdmin
            // The most recent logins, not the whole journal.
            ? MapLoginHistory(await _loginRecordService.GetHistory(user.UserId, new PagingQuery { Take = 50 }))
            : null;

        // Info and Contacts stay unmapped: the panel sits in the profile page, whose
        // community endpoint already carries them.
        var profile = _mapper.ToModeratedProfile(user);

        profile.Email = isAdmin ? user.Email : null;
        profile.IpAddresses = ipAddresses;
        profile.LoginHistory = loginHistory;

        profile.LinkedProfiles = MapLinkedProfiles(linkedProfiles);
        profile.ModeratorNotes = MapModeratorNotes(moderatorNotes, caller.UserId, isSeniorMod);

        if (personalNote != null)
        {
            profile.PersonalNote = _mapper.ToPersonalNote(personalNote);
        }

        profile.Violations = Violations(warnings, banStatus);
        profile.Permissions = Permissions(isAdmin, isSeniorMod, ModerationRank.Reaches(callerRole, user.Role));

        return profile;
    }

    /// <summary>
    /// The violation summary of the profile: its warnings and its bans.
    /// </summary>
    private static ViolationSummary Violations(UserWarningsInfo warnings, UserBanStatus banStatus)
    {
        return new ViolationSummary
        {
            TotalWarnings = warnings.ActiveCount,
            ActiveWarningPoints = warnings.TotalPoints,
            TotalBans = banStatus.History?.Count() ?? 0,
            IsCurrentlyBanned = banStatus.IsBanned,
            CurrentBanEndUtc = banStatus.ActiveBan?.ExpiresUtc,
            // The reason as it was written: the panel prints it as text.
            CurrentBanReason = banStatus.ActiveBan?.Comment.Value
        };
    }

    /// <summary>
    /// What the caller may see and do on the profile.
    /// </summary>
    /// <remarks>
    /// The rank reach is the formula the warning and ban services refuse by, so no
    /// button is drawn where the write would come back forbidden.
    /// </remarks>
    private static ModerationPermissions Permissions(bool isAdmin, bool isSeniorMod, bool reachesByRank) =>
        new()
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

    public async Task<UserProfile> ModerateUserProfile(string username, ModerateProfile profile)
    {
        var updatedUser = await _moderatedProfileService.ModerateProfile(username, profile.Info ?? string.Empty);
        return _mapper.ToUserProfile(updatedUser);
    }

    public async Task<Envelope<ModeratedProfile>> SetModerationWatch(string username, bool underWatch)
    {
        await _moderatedProfileService.SetModerationWatch(username, underWatch);
        // Re-read, so the answer is the payload the page was rendered from.
        return new Envelope<ModeratedProfile>(await GetModeratedProfile(username));
    }

    public async Task<UserProfile> SetUserRole(string username, UserRole role)
    {
        await _moderatedProfileService.SetUserRole(username, role);
        var user = await _moderatedProfileService.GetProfile(username);
        return _mapper.ToUserProfile(user);
    }

    public Task RemoveAccount(string username, RemoveAccountRequest request) =>
        _deactivationService.DeactivateByModeration(username, request.Reason);

    public Task RestoreAccount(string username) =>
        _deactivationService.RestoreByModeration(username);

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
    /// The address is carried as the service handed it: empty below an administrator.
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

    private static IReadOnlyList<ApiUserIpInfo> MapIpAddresses(IReadOnlyList<ServiceUserIpInfo> ips) =>
        ips.Select(ip => new ApiUserIpInfo
        {
            IpAddress = ip.IpAddress,
            FirstSeenUtc = ip.FirstSeenUtc,
            LastSeenUtc = ip.LastSeenUtc,
            LoginsCount = ip.LoginsCount
        }).ToList();

    private static IReadOnlyList<LoginRecord> MapLoginHistory(IReadOnlyList<ServiceUserLoginRecord> records) =>
        records.Select(r => new LoginRecord
        {
            LoginUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
            IsSuccessful = r.IsSuccessful
        }).ToList();

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
