using System;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Warnings;
using NSubstitute;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// The mapper the ban rules read "banned right now" through, built around a
/// clock that answers with a moment of the test's choosing.
/// </summary>
internal static class WarningMappers
{
    /// <summary>A mapper whose clock reports the given moment.</summary>
    internal static WarningMapper At(DateTimeOffset moment)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(moment);

        return new WarningMapper(new UserMapper(), clock);
    }
}
