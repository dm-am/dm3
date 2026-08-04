using System;
using DM.Infrastructure.Core.Clock;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Core.Tests.Clock;

public class DateTimeProviderShould
{
    [Fact]
    public void ProvideCurrentMomentUtcDate()
    {
        var actual = new DateTimeProvider().Now;
        actual.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}