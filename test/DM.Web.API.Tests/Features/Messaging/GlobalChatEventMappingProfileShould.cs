using System;
using AutoMapper;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Messaging.GlobalChatEvents;
using DM.Web.API.Shared.BbRendering;
using FluentAssertions;
using Xunit;
using ServiceGlobalChatEvent = DM.Domain.Messaging.Features.GlobalChatEvents.GlobalChatEvent;

namespace DM.Web.API.Tests.Features.Messaging;

public class GlobalChatEventMappingProfileShould : UnitTestBase
{
    private static MapperConfiguration Configuration => new(cfg =>
    {
        cfg.AddProfile<UserMappingProfile>();
        cfg.AddProfile<BbTextMappingProfile>();
        cfg.AddProfile<GlobalChatEventMappingProfile>();
    });

    /// <summary>
    /// The avatar converter is the one type in these profiles that takes a
    /// dependency. No author is mapped below, so it is never reached, but the
    /// factory still has to be able to answer for it.
    /// </summary>
    private IMapper Mapper => Configuration.CreateMapper(type =>
        type == typeof(AvatarPictureConverter)
            ? new AvatarPictureConverter(Mock<IImgproxyUrlBuilder>().Object)
            : Activator.CreateInstance(type)!);

    [Fact]
    public void HaveValidConfiguration() => Configuration.AssertConfigurationIsValid();

    /// <summary>
    /// The planned start and the actual one are two different facts, and only the
    /// planned one used to cross the API. A start is manual: an event that went
    /// live late has a StartsUtc well in the past and is still running, so a
    /// client holding the plan and the duration computes an end that has already
    /// passed. Both marks cross, or nothing on the other side can say how long an
    /// event has been going or how much of it is left.
    /// </summary>
    [Fact]
    public void CarryBothActualMarksOfTheEvent()
    {
        var planned = new DateTimeOffset(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);
        var chatEvent = new ServiceGlobalChatEvent
        {
            Id = Guid.NewGuid(),
            Title = "Вечер быстрых зарисовок",
            Description = string.Empty,
            StartsUtc = planned,
            StartedUtc = planned.AddMinutes(37),
            EndedUtc = planned.AddHours(3)
        };

        var mapped = Mapper.Map<GlobalChatEvent>(chatEvent);

        mapped.StartedUtc.Should().Be(planned.AddMinutes(37));
        mapped.EndedUtc.Should().Be(planned.AddHours(3));
    }
}
