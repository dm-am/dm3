using System;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

// The converter renders through BbParserWrapper and casts what the provider hands it, so every
// surface gets a wrapper for every audience it renders.
public class BbConverterParserShould
{
    private readonly BbParserProvider _provider = new();

    public static TheoryData<BbSurface> Surfaces => new(Enum.GetValues<BbSurface>());

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void HandOutTheWrapperForEveryAudience(BbSurface surface)
    {
        _provider.GetForSurface(surface).Should().BeOfType<BbParserWrapper>("the display render casts to it");
        _provider.GetSafeForSurface(surface).Should().BeOfType<BbParserWrapper>("the embed render casts to it");
    }
}
