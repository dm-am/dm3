using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Community.Features.Achievements;
using DM.Domain.Community.Features.Awards;
using DM.Domain.Community.Features.Catalogs;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using SeededAchievementType = DM.Infrastructure.Persistence.Entities.Community.AchievementType;
using SeededAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;

namespace DM.Domain.Community.Tests.Features.Catalogs;

// Read out of the seed, so a tier added to a catalog past the scale fails here, and so does a chain
// whose metals no longer follow its thresholds.
public class TierScaleShould
{
    private static IReadOnlyList<IDictionary<string, object?>> SeedRows<TEntity>()
    {
        // Model metadata only: the connection is never opened.
        var options = new DbContextOptionsBuilder<DmDbContext>()
            .UseNpgsql("Host=localhost;Database=tier-probe;Username=probe;Password=probe")
            .Options;
        using var context = new DmDbContext(options);

        // HasData lives in the design-time model; the runtime one drops it.
        return context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(TEntity))!
            .GetSeedData()
            .ToList();
    }

    private static IReadOnlyCollection<int> SeededTiers<TEntity>() =>
        SeedRows<TEntity>()
            .Select(row => row["Tier"])
            .OfType<int>()
            .Distinct()
            .OrderBy(tier => tier)
            .ToList();

    public static TheoryData<int> EverySeededTier()
    {
        var data = new TheoryData<int>();
        foreach (var tier in SeededTiers<SeededAwardType>().Union(SeededTiers<SeededAchievementType>()))
        {
            data.Add(tier);
        }

        return data;
    }

    [Fact]
    public void BeUsedByBothSeededCatalogs()
    {
        SeededTiers<SeededAwardType>().Should().NotBeEmpty();
        SeededTiers<SeededAchievementType>().Should().NotBeEmpty();
    }

    [Theory]
    [MemberData(nameof(EverySeededTier))]
    public void AcceptEveryTierTheSeededCatalogsUse(int tier)
    {
        new CreateAwardTypeValidator().TestValidate(CreateAward(tier)).ShouldNotHaveValidationErrorFor(t => t.Tier);
        new UpdateAwardTypeValidator().TestValidate(UpdateAward(tier)).ShouldNotHaveValidationErrorFor(t => t.Tier);
        new CreateAchievementTypeValidator().TestValidate(CreateAchievement(tier))
            .ShouldNotHaveValidationErrorFor(t => t.Tier);
        new UpdateAchievementTypeValidator().TestValidate(UpdateAchievement(tier))
            .ShouldNotHaveValidationErrorFor(t => t.Tier);
    }

    [Theory]
    [InlineData(TierScale.MinTier - 1)]
    [InlineData(TierScale.MaxTier + 1)]
    public void RejectATierOffTheScale(int tier)
    {
        new CreateAwardTypeValidator().TestValidate(CreateAward(tier)).ShouldHaveValidationErrorFor(t => t.Tier);
        new UpdateAwardTypeValidator().TestValidate(UpdateAward(tier)).ShouldHaveValidationErrorFor(t => t.Tier);
        new CreateAchievementTypeValidator().TestValidate(CreateAchievement(tier))
            .ShouldHaveValidationErrorFor(t => t.Tier);
        new UpdateAchievementTypeValidator().TestValidate(UpdateAchievement(tier))
            .ShouldHaveValidationErrorFor(t => t.Tier);
    }

    [Fact]
    public void ClimbBronzeSilverGoldPlatinumAlongEveryAchievementChain()
    {
        var chains = SeedRows<SeededAchievementType>()
            .GroupBy(row => (Guid)row["AchievementCategoryId"]!)
            .ToList();
        chains.Should().NotBeEmpty();

        foreach (var chain in chains)
        {
            chain.OrderBy(row => (int)row["Threshold"]!)
                .Select(row => (int?)row["Tier"])
                .Should().Equal(
                    [TierScale.Bronze, TierScale.Silver, TierScale.Gold, TierScale.Platinum],
                    $"the tiles of chain {chain.Key} take their metal from the tier, in the order of the thresholds");
        }
    }

    private static CreateAwardType CreateAward(int tier) => new()
    {
        Code = "tier_probe",
        Title = "Probe",
        Description = "Probe",
        IconName = "trophy-cup",
        Tier = tier,
        SortOrder = 1
    };

    private static UpdateAwardType UpdateAward(int tier) => new() { Id = Guid.NewGuid(), Title = "Probe", Tier = tier };

    private static CreateAchievementType CreateAchievement(int tier) => new()
    {
        Code = "TIER_PROBE",
        Title = "Probe",
        Threshold = 1,
        Tier = tier,
        AchievementCategoryId = Guid.NewGuid()
    };

    private static UpdateAchievementType UpdateAchievement(int tier) => new() { Id = Guid.NewGuid(), Tier = tier };
}
