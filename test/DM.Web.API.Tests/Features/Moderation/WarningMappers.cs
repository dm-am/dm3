using System;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Warnings;
using NSubstitute;

namespace DM.Web.API.Tests.Features.Moderation;

internal static class WarningMappers
{
    internal static WarningMapper At(DateTimeOffset moment)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(moment);

        return new WarningMapper(new UserMapper(), clock);
    }
}
