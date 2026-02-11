using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Authentication.Repositories;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Dto.Enums;
using System.Net;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Moderation;

namespace DM.Web.API.Services.Moderation;

/// <inheritdoc />
internal class ModerationProfileApiService : IModerationProfileApiService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserReadingService _userReadingService;
    private readonly ILoginRecordRepository _loginRecordRepository;
    private readonly IWarningApiService _warningApiService;
    private readonly IBanApiService _banApiService;
    private readonly IProfileModNoteApiService _modNoteApiService;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ModerationProfileApiService(
        IIdentityProvider identityProvider,
        IUserReadingService userReadingService,
        ILoginRecordRepository loginRecordRepository,
        IWarningApiService warningApiService,
        IBanApiService banApiService,
        IProfileModNoteApiService modNoteApiService,
        IDateTimeProvider dateTimeProvider)
    {
        _identityProvider = identityProvider;
        _userReadingService = userReadingService;
        _loginRecordRepository = loginRecordRepository;
        _warningApiService = warningApiService;
        _banApiService = banApiService;
        _modNoteApiService = modNoteApiService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Envelope<ModerationProfileDto>> GetModerationProfile(string login)
    {
        var caller = _identityProvider.Current.User;
        var callerRole = caller.Role;
        var isAdmin = callerRole >= UserRole.Admin;
        var isSeniorMod = callerRole >= UserRole.SeniorModerator;

        // Get target user
        var user = await _userReadingService.GetDetails(login);
        if (user == null)
            throw new HttpException(HttpStatusCode.NotFound, $"User '{login}' not found");

        // Run queries in parallel — conditional on role
        var linkedProfilesTask = _loginRecordRepository.GetLinkedProfiles(user.UserId);
        var modNotesTask = _modNoteApiService.GetNotes(login);
        var warningsTask = _warningApiService.GetUserWarnings(login);
        var banStatusTask = _banApiService.GetUserBanStatus(login);

        var ipsTask = isAdmin
            ? _loginRecordRepository.GetUserIps(user.UserId)
            : Task.FromResult<IReadOnlyList<UserIpInfo>>(null!);
        var loginHistoryTask = isAdmin
            ? _loginRecordRepository.GetLoginHistory(user.UserId)
            : Task.FromResult<IReadOnlyList<UserLoginRecord>>(null!);

        await Task.WhenAll(
            linkedProfilesTask, modNotesTask, warningsTask,
            banStatusTask, ipsTask, loginHistoryTask);

        var warningsInfo = warningsTask.Result;
        var banStatus = banStatusTask.Result;
        var modNotes = modNotesTask.Result;

        return new Envelope<ModerationProfileDto>(new ModerationProfileDto
        {
            Login = user.Login,
            UserId = user.UserId,
            RegistrationDateUtc = user.CreatedUtc,

            // Admin-only fields (null for non-admin callers)
            Email = isAdmin ? user.Email : null,
            IpAddresses = isAdmin ? MapIpInfos(ipsTask.Result) : null,
            LoginHistory = isAdmin ? MapLoginHistory(loginHistoryTask.Result) : null,

            // Moderator+ fields
            LinkedProfiles = MapLinkedProfiles(linkedProfilesTask.Result),
            ModeratorNotes = MapModNotes(modNotes.Resources, caller.UserId, isSeniorMod),

            Violations = new ViolationSummaryDto
            {
                TotalWarnings = warningsInfo.ActiveCount,
                ActiveWarningPoints = warningsInfo.TotalPoints,
                TotalBans = banStatus.History?.Count() ?? 0,
                IsCurrentlyBanned = banStatus.IsBanned,
                CurrentBanEndUtc = banStatus.ActiveBan?.ExpiresUtc,
                CurrentBanReason = banStatus.ActiveBan?.Comment
            },

            Permissions = new ModerationPermissionsDto
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
            }
        });
    }

    private static IReadOnlyList<UserIpInfoDto> MapIpInfos(IReadOnlyList<UserIpInfo> ips)
    {
        if (ips == null) return [];
        return ips.Select(ip => new UserIpInfoDto
        {
            IpAddress = ip.IpAddress,
            FirstSeenUtc = ip.FirstSeenUtc,
            LastSeenUtc = ip.LastSeenUtc,
            LoginCount = ip.LoginCount
        }).ToList();
    }

    private static IReadOnlyList<LoginRecordDto> MapLoginHistory(IReadOnlyList<UserLoginRecord> records)
    {
        if (records == null) return [];
        return records.Select(r => new LoginRecordDto
        {
            LoginUtc = r.LoginUtc,
            IpAddress = r.IpAddress,
            UserAgent = r.UserAgent,
            IsSuccessful = r.IsSuccessful
        }).ToList();
    }

    private static IReadOnlyList<LinkedProfileDto> MapLinkedProfiles(IReadOnlyList<LinkedProfile> profiles)
    {
        return profiles.Select(p => new LinkedProfileDto
        {
            UserId = p.UserId,
            Login = p.Login,
            SharedIpCount = p.SharedIpCount,
            LastSharedLoginUtc = p.LastSharedLoginUtc
        }).ToList();
    }

    private static IReadOnlyList<ModNoteDto> MapModNotes(
        IEnumerable<Dto.Moderation.ProfileModNote> notes,
        Guid callerId,
        bool isSeniorMod)
    {
        return notes.Select(n => new ModNoteDto
        {
            NoteId = n.Id,
            AuthorLogin = n.Author?.Login ?? string.Empty,
            AuthorId = n.Author?.Id ?? Guid.Empty,
            Text = n.Text,
            CreatedUtc = n.CreatedAtUtc,
            UpdatedUtc = n.ModifiedAtUtc,
            CanEdit = isSeniorMod || (n.Author?.Id == callerId),
            CanDelete = isSeniorMod || (n.Author?.Id == callerId)
        }).ToList();
    }
}
