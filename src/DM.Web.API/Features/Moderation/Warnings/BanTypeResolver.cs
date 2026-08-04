using System;
using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Features.Moderation.Bans;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// Restores the API ban type from the stored ban.
/// </summary>
/// <remarks>
/// Permanence has no column of its own — it is carried by an end date far in the
/// future — so reading it back is a comparison against a threshold, and the
/// threshold belongs next to the length a permanent ban is written with, on the
/// domain ban. The inline version here compared against a literal that its own
/// comment contradicted, off a clock the rest of the ban code does not use.
/// </remarks>
internal class BanTypeResolver :
    IValueResolver<DomainBan, Ban, BanType>,
    IValueResolver<DomainBan, PublicBan, BanType>
{
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public BanTypeResolver(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public BanType Resolve(DomainBan source, Ban destination, BanType destMember, ResolutionContext context) =>
        MapBanType(source, _dateTimeProvider.Now);

    /// <inheritdoc />
    public BanType Resolve(DomainBan source, PublicBan destination, BanType destMember, ResolutionContext context) =>
        MapBanType(source, _dateTimeProvider.Now);

    private static BanType MapBanType(DomainBan ban, DateTimeOffset moment)
    {
        if (ban.IsVoluntary)
        {
            return BanType.Voluntary;
        }

        return ban.IsPermanentAt(moment) ? BanType.Permanent : BanType.Temporary;
    }
}
