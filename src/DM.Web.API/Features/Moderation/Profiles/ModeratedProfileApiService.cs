using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Profiles;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Features.Community.Users;
using ApiUserIpInfo = DM.Web.API.Features.Moderation.Profiles.UserIpInfo;
using ApiLinkedProfile = DM.Web.API.Features.Moderation.Profiles.LinkedProfile;
using ServiceUserIpInfo = DM.Domain.Account.Features.Authentication.UserIpInfo;
using ServiceLinkedProfile = DM.Domain.Account.Features.Authentication.LinkedProfile;
using ServiceUserLoginRecord = DM.Domain.Account.Features.Authentication.UserLoginRecord;

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
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ModeratedProfileApiService(
        IIdentityProvider identityProvider,
        IModeratedProfileService moderatedProfileService,
        ILoginRecordService loginRecordService,
        DM.Web.API.Features.Moderation.Warnings.IWarningApiService warningApiService,
        DM.Web.API.Features.Moderation.Bans.IBanApiService banApiService,
        IModeratedProfileNoteApiService moderatorNoteApiService,
        IUserProfileNoteService personalNoteService,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper)
    {
        _identityProvider = identityProvider;
        _moderatedProfileService = moderatedProfileService;
        _loginRecordService = loginRecordService;
        _warningApiService = warningApiService;
        _banApiService = banApiService;
        _moderatorNoteApiService = moderatorNoteApiService;
        _personalNoteService = personalNoteService;
        _dateTimeProvider = dateTimeProvider;
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
            ? MapLoginHistory(await _loginRecordService.GetHistory(user.UserId))
            : null;

        // Map base UserProfile fields using AutoMapper, then add moderation-specific fields
        var profile = _mapper.Map<ModeratedProfile>(user);

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
            profile.PersonalNote = new PersonalNote
            {
                Id = personalNote.Id,
                Text = personalNote.Text,
                CreatedUtc = personalNote.CreatedUtc,
                ModifiedUtc = personalNote.ModifiedUtc
            };
        }

        profile.Violations = new ViolationSummary
        {
            TotalWarnings = warnings.ActiveCount,
            ActiveWarningPoints = warnings.TotalPoints,
            TotalBans = banStatus.History?.Count() ?? 0,
            IsCurrentlyBanned = banStatus.IsBanned,
            CurrentBanEndUtc = banStatus.ActiveBan?.ExpiresUtc,
            CurrentBanReason = banStatus.ActiveBan?.Comment
        };

        profile.Permissions = new ModerationPermissions
        {
            CanViewEmail = isAdmin,
            CanViewIpAddresses = isAdmin,
            CanViewLoginHistory = isAdmin,
            CanViewLinkedProfiles = true, // Controller already requires Moderator+
            CanViewModNotes = true,
            CanCreateModNote = true,
            CanIssueWarning = true,
            CanIssueBan = isSeniorMod,
            CanLiftBan = isSeniorMod
        };

        return profile;
    }

    /// <inheritdoc />
    public async Task<UserProfile> ModerateUserProfile(string username, ModerateProfile profile)
    {
        var updatedUser = await _moderatedProfileService.ModerateProfile(username, profile.Info ?? string.Empty);
        return _mapper.Map<UserProfile>(updatedUser);
    }

    /// <inheritdoc />
    public async Task<UserProfile> SetUserRole(string username, UserRole role)
    {
        await _moderatedProfileService.SetUserRole(username, role);
        var user = await _moderatedProfileService.GetProfile(username);
        return _mapper.Map<UserProfile>(user);
    }

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
