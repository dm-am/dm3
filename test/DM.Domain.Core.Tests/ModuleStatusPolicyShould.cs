using System;
using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Core.Tests;

/// <summary>
/// The status machine a game and a blog both run on.
/// </summary>
/// <remarks>
/// Every field of the answer is asserted, including the ones the move leaves
/// alone. Null there is not "nothing happened": it is what tells the repository
/// to keep the stored value, and the module tests above this one could not see
/// the difference — a policy that started writing ClosedReason.None on an
/// activation would have passed them all.
/// </remarks>
public class ModuleStatusPolicyShould
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    private static ModuleLifecycle Module(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null) =>
        new(status, closedReason, activatedUtc, closedUtc);

    [Fact]
    public void ActivateADraftAndStampTheFirstActivation()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Start, Module(ModuleStatus.Draft), Now);

        change.Status.Should().Be(ModuleStatus.Active);
        change.ActivatedUtc.Should().Be(Now);
        change.ClosedReason.Should().BeNull("a module that starts has no reason to be closed for, " +
            "and the stored one is not rewritten");
        change.ClosedUtc.Should().BeNull();
        change.ClearClosedUtc.Should().BeFalse();
    }

    [Fact]
    public void KeepTheFirstActivationWhenADraftIsStartedAgain()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Start,
            Module(ModuleStatus.Draft, activatedUtc: Now.AddMonths(-1)),
            Now);

        change.Status.Should().Be(ModuleStatus.Active);
        change.ActivatedUtc.Should().BeNull();
    }

    [Theory]
    [InlineData(ModuleStatus.Active)]
    [InlineData(ModuleStatus.Closed)]
    public void RefuseAStartFromAnythingButADraft(ModuleStatus status)
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Start, Module(status), Now);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public void FreezeAnActiveModuleAndStampTheMomentItStopped()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Freeze, Module(ModuleStatus.Active), Now);

        change.Status.Should().Be(ModuleStatus.Closed);
        change.ClosedReason.Should().Be(ClosedReason.Frozen);
        change.ClosedUtc.Should().Be(Now);
        change.ActivatedUtc.Should().BeNull();
        change.ClearClosedUtc.Should().BeFalse();
    }

    [Fact]
    public void FinishAnActiveModuleAndStampTheMomentItStopped()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Finish, Module(ModuleStatus.Active), Now);

        change.Status.Should().Be(ModuleStatus.Closed);
        change.ClosedReason.Should().Be(ClosedReason.Finished);
        change.ClosedUtc.Should().Be(Now);
        change.ActivatedUtc.Should().BeNull();
        change.ClearClosedUtc.Should().BeFalse();
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Closed)]
    public void RefuseAFreezeOrAFinishFromAnythingButAnActiveModule(ModuleStatus status)
    {
        var freeze = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Freeze, Module(status), Now);
        var finish = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Finish, Module(status), Now);

        freeze.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
        finish.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public void CloseAnActiveModuleWithNoReasonAndStampIt()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Close, Module(ModuleStatus.Active), Now);

        change.Status.Should().Be(ModuleStatus.Closed);
        change.ClosedReason.Should().Be(ClosedReason.None);
        change.ClosedUtc.Should().Be(Now);
        change.ActivatedUtc.Should().BeNull();
        change.ClearClosedUtc.Should().BeFalse();
    }

    [Fact]
    public void CloseAFrozenModuleWithoutRestampingTheMomentItStopped()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Close,
            Module(ModuleStatus.Closed, ClosedReason.Frozen, closedUtc: Now.AddDays(-7)),
            Now);

        change.Status.Should().Be(ModuleStatus.Closed);
        change.ClosedReason.Should().Be(ClosedReason.None);
        change.ClosedUtc.Should().BeNull();
    }

    [Fact]
    public void RefuseToCloseAFinishedModule()
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Close,
            Module(ModuleStatus.Closed, ClosedReason.Finished, closedUtc: Now.AddDays(-7)),
            Now);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public void RefuseToCloseADraft()
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Close, Module(ModuleStatus.Draft), Now);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(ClosedReason.None)]
    [InlineData(ClosedReason.Finished)]
    [InlineData(ClosedReason.Frozen)]
    public void ReopenAClosedModuleWhateverItWasClosedFor(ClosedReason closedReason)
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Reopen,
            Module(ModuleStatus.Closed, closedReason,
                activatedUtc: Now.AddMonths(-2), closedUtc: Now.AddDays(-7)),
            Now);

        change.Status.Should().Be(ModuleStatus.Active);
        change.ClosedReason.Should().Be(ClosedReason.None);
        change.ClearClosedUtc.Should().BeTrue();
        change.ClosedUtc.Should().BeNull();
        change.ActivatedUtc.Should().BeNull("it was active once already");
    }

    [Fact]
    public void StampTheActivationWhenAModuleClosedBeforeItEverStartedIsReopened()
    {
        var change = ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Reopen,
            Module(ModuleStatus.Closed, closedUtc: Now.AddDays(-7)),
            Now);

        change.ActivatedUtc.Should().Be(Now);
        change.ClearClosedUtc.Should().BeTrue();
    }

    [Theory]
    [InlineData(ModuleStatus.Draft)]
    [InlineData(ModuleStatus.Active)]
    public void RefuseAReopenOfSomethingThatIsNotClosed(ModuleStatus status)
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Reopen, Module(status), Now);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public void NameTheMoveAndTheStateItWasRefusedFrom()
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Freeze, Module(ModuleStatus.Draft), Now);

        act.Should().Throw<HttpException>()
            .WithMessage("Переход \"Freeze\" недоступен из статуса \"Draft\"");
    }

    [Fact]
    public void NameTheReasonAClosedModuleIsClosedFor()
    {
        var act = () => ModuleStatusPolicy.Resolve(
            ModuleStatusTransition.Close,
            Module(ModuleStatus.Closed, ClosedReason.Finished, closedUtc: Now.AddDays(-7)),
            Now);

        act.Should().Throw<HttpException>()
            .WithMessage("Переход \"Close\" недоступен из статуса \"Closed\" (Finished)");
    }

    [Fact]
    public void RefuseAMoveOutsideTheFiveItKnows()
    {
        var act = () => ModuleStatusPolicy.Resolve(
            (ModuleStatusTransition)42, Module(ModuleStatus.Draft), Now);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage(RefusalMessage.UnknownStatusTransition);
    }
}
