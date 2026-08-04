using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Community.Features.Awards;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using SeededAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;

namespace DM.Domain.Community.Tests.Features.Awards;

/// <summary>
/// The accepted tier range and the tiers the seeded catalog actually ships must
/// agree. They did not: the catalog carries a place-less honorary record at tier
/// 5 while both validators cut the range at 4, and the moderation dialog always
/// resends the current tier — so that record answered 400 to every edit,
/// including edits that never touched the tier.
/// </summary>
/// <remarks>
/// The expected values are read out of the seed rather than repeated here, so a
/// tier added to the catalog fails this test instead of making one more catalog
/// record uneditable in production.
/// </remarks>
public class AwardTypeTierRangeShould
{
    private static IReadOnlyCollection<int> SeededTiers()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=award-tier-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);

        // HasData lives in the design-time model; the runtime one drops it.
        return context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(SeededAwardType))!
            .GetSeedData()
            .Select(row => row[nameof(SeededAwardType.Tier)])
            .OfType<int>()
            .Distinct()
            .OrderBy(tier => tier)
            .ToList();
    }

    private static CreateAwardType CreateWithTier(int tier) => new()
    {
        Code = "tier_probe",
        Title = "Probe",
        Description = "Probe",
        IconName = "trophy-cup",
        Tier = tier,
        SortOrder = 1
    };

    private static UpdateAwardType UpdateWithTier(int tier) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Probe",
        Tier = tier
    };

    [Fact]
    public void AcceptEveryTierTheSeededCatalogUses()
    {
        var seeded = SeededTiers();
        seeded.Should().NotBeEmpty("the seeded catalog assigns tiers, so there is a range to agree with");

        var createValidator = new CreateAwardTypeValidator();
        var updateValidator = new UpdateAwardTypeValidator();

        foreach (var tier in seeded)
        {
            createValidator.TestValidate(CreateWithTier(tier))
                .ShouldNotHaveValidationErrorFor(t => t.Tier);
            updateValidator.TestValidate(UpdateWithTier(tier))
                .ShouldNotHaveValidationErrorFor(t => t.Tier);
        }
    }

    [Fact]
    public void RejectATierTheCatalogDoesNotReach()
    {
        var seeded = SeededTiers();
        var createValidator = new CreateAwardTypeValidator();
        var updateValidator = new UpdateAwardTypeValidator();

        foreach (var tier in new[] { seeded.Min() - 1, seeded.Max() + 1 })
        {
            createValidator.TestValidate(CreateWithTier(tier))
                .ShouldHaveValidationErrorFor(t => t.Tier);
            updateValidator.TestValidate(UpdateWithTier(tier))
                .ShouldHaveValidationErrorFor(t => t.Tier);
        }
    }
}
