using System;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Bans;
using Riok.Mapperly.Abstractions;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;
using DomainWarning = DM.Domain.Moderation.Features.Warnings.Warning;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Compile-time mapper for warnings and bans. The clock-dependent fields the
/// AutoMapper profile filled through BanTypeResolver/BanActivityResolver are
/// ordinary methods here, and both keep delegating the actual predicates to
/// the domain ban - the single definition of "permanent" and "banned right
/// now" that enforcement uses.
/// </summary>
[Mapper]
internal partial class WarningMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    private readonly IDateTimeProvider _dateTimeProvider;

    public WarningMapper(UserMapper userMapper, IDateTimeProvider dateTimeProvider)
    {
        _userMapper = userMapper;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Domain warning to the moderator-facing DTO, evidence trio included
    /// </summary>
    public Warning ToWarning(DomainWarning warning)
    {
        var result = ToWarningCore(warning);
        result.EntityId = warning.EntityId != Guid.Empty ? warning.EntityId : null;
        result.IsActive = !warning.IsRemoved;
        return result;
    }

    /// <summary>
    /// Domain warning to the public view: no reason, no moderator identity,
    /// no causation refs - a stranger sees points and dates
    /// </summary>
    public PublicWarning ToPublicWarning(DomainWarning warning)
    {
        var result = ToPublicWarningCore(warning);
        result.IsActive = !warning.IsRemoved;
        return result;
    }

    /// <summary>
    /// Domain ban to the moderator-facing DTO
    /// </summary>
    public Ban ToBan(DomainBan ban)
    {
        var result = ToBanCore(ban);
        var now = _dateTimeProvider.Now;
        result.Type = ResolveBanType(ban, now);
        result.IsActive = ban.IsInForceAt(now);
        return result;
    }

    /// <summary>
    /// Domain ban to the public view
    /// </summary>
    public PublicBan ToPublicBan(DomainBan ban)
    {
        var result = ToPublicBanCore(ban);
        var now = _dateTimeProvider.Now;
        result.Type = ResolveBanType(ban, now);
        result.IsActive = ban.IsInForceAt(now);
        return result;
    }

    [MapProperty(nameof(DomainWarning.WarningId), nameof(Warning.Id))]
    [MapProperty(nameof(DomainWarning.TargetUser), nameof(Warning.User))]
    [MapProperty(nameof(DomainWarning.Author), nameof(Warning.Moderator))]
    [MapProperty(nameof(DomainWarning.Text), nameof(Warning.Reason))]
    [MapperIgnoreTarget(nameof(Warning.EntityId))]
    [MapperIgnoreTarget(nameof(Warning.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Warning ToWarningCore(DomainWarning warning);

    [MapperIgnoreTarget(nameof(PublicWarning.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PublicWarning ToPublicWarningCore(DomainWarning warning);

    [MapProperty(nameof(DomainBan.BanId), nameof(Ban.Id))]
    [MapProperty(nameof(DomainBan.TargetUser), nameof(Ban.User))]
    [MapProperty(nameof(DomainBan.Author), nameof(Ban.Moderator))]
    [MapProperty(nameof(DomainBan.EndedUtc), nameof(Ban.ExpiresUtc))]
    [MapperIgnoreTarget(nameof(Ban.Type))]
    [MapperIgnoreTarget(nameof(Ban.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial Ban ToBanCore(DomainBan ban);

    [MapProperty(nameof(DomainBan.EndedUtc), nameof(PublicBan.ExpiresUtc))]
    [MapperIgnoreTarget(nameof(PublicBan.Type))]
    [MapperIgnoreTarget(nameof(PublicBan.IsActive))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PublicBan ToPublicBanCore(DomainBan ban);

    // Permanence has no column of its own - it is carried by an end date far
    // in the future - so reading it back is a comparison against a threshold
    // that lives on the domain ban, next to the length a permanent ban is
    // written with.
    private static BanType ResolveBanType(DomainBan ban, DateTimeOffset moment)
    {
        if (ban.IsVoluntary)
        {
            return BanType.Voluntary;
        }

        return ban.IsPermanentAt(moment) ? BanType.Permanent : BanType.Temporary;
    }
}
