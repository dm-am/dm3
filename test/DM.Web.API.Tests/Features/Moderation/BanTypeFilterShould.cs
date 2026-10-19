using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Features.Moderation.Bans;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainBan = DM.Domain.Moderation.Features.Bans.Ban;
using DM.Domain.Moderation.Features.Bans;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// GET /v1/bans?type=... answers with bans of that type.
/// </summary>
/// <remarks>The type is derived during mapping, so the filter runs after it.</remarks>
public class BanTypeFilterShould
{
    private static readonly DateTimeOffset Moment = new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);


    private static readonly Guid TemporaryId = Guid.Parse("a1c3f5e7-0000-4000-8000-000000000001");
    private static readonly Guid PermanentId = Guid.Parse("a1c3f5e7-0000-4000-8000-000000000002");
    private static readonly Guid VoluntaryId = Guid.Parse("a1c3f5e7-0000-4000-8000-000000000003");

    private static BanApiService Service()
    {
        var banService = Substitute.For<IBanService>();
        banService
            .GetAllActiveBans(Arg.Any<CancellationToken>()).Returns(new List<DomainBan>
            {
                new()
                {
                    BanId = TemporaryId,
                    StartedUtc = Moment,
                    EndedUtc = Moment.AddDays(30)
                },
                new()
                {
                    BanId = PermanentId,
                    StartedUtc = Moment,
                    EndedUtc = Moment.AddYears(DomainBan.PermanentYears)
                },
                new()
                {
                    BanId = VoluntaryId,
                    StartedUtc = Moment,
                    EndedUtc = Moment.AddYears(DomainBan.PermanentYears),
                    IsVoluntary = true
                }
            });

        return new BanApiService(banService, WarningMappers.At(Moment));
    }

    [Theory]
    [InlineData(BanType.Temporary)]
    [InlineData(BanType.Permanent)]
    [InlineData(BanType.Voluntary)]
    public async Task AnswerWithTheAskedTypeAlone(BanType type)
    {
        var expected = type switch
        {
            BanType.Temporary => TemporaryId,
            BanType.Permanent => PermanentId,
            _ => VoluntaryId
        };

        var bans = (await Service().GetAllActiveBans(type)).Resources.ToArray();

        bans.Should().ContainSingle().Which.Id.Should().Be(expected,
            "a filter that returns everything tells a moderator the opposite of what he asked");
    }

    [Fact]
    public async Task AnswerWithEveryBanWhenNoTypeIsAsked()
    {
        var bans = (await Service().GetAllActiveBans()).Resources.ToArray();

        bans.Should().HaveCount(3, "the parameter is optional and omitting it filters nothing");
    }
}
