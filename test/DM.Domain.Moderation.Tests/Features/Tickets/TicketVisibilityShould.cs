using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketVisibilityShould : TicketServiceTestBase
{
    [Theory]
    [InlineData(TicketSubtype.AccountRemoval)]
    [InlineData(TicketSubtype.AccountRestoration)]
    [InlineData(TicketSubtype.PersonalData)]
    [InlineData(TicketSubtype.AgreementQuestion)]
    public async Task ScopeAccountSubtypesToAdministrators(TicketSubtype subtype)
    {
        // Support-desk scope, like AccessRecovery: these are claims about who
        // owns an account, and the queue that reads them is the administrators'
        // one. A moderator asking for the category by hand gets an empty page
        // rather than a refusal, exactly as for any subtype out of scope.
        SetCurrentUser(UserRole.SeniorModerator);
        var (moderatorTickets, _) = await service.GetTickets(new PagingQuery(), subtype: subtype);

        moderatorTickets.Should().BeEmpty();
        await ticketRepository.DidNotReceiveWithAnyArgs().GetTickets(default!, default, default, default, default);

        // The administrator queue passes the category through to the repository:
        // the request does arrive at moderation, it is only scoped.
        IReadOnlyCollection<TicketSubtype>? capturedSubtypes = null;
        QueueAnswersEmpty(subtypes: s => capturedSubtypes = s);

        SetCurrentUser(UserRole.Admin);
        await service.GetTickets(new PagingQuery(), subtype: subtype);

        capturedSubtypes.Should().BeEquivalentTo(new[] { subtype });
    }

    [Fact]
    public async Task FilterTicketSubtypesByCallerRole()
    {
        IReadOnlyCollection<TicketSubtype>? capturedSubtypes = null;
        QueueAnswersEmpty(subtypes: s => capturedSubtypes = s);

        // Junior moderator: user complaints only
        SetCurrentUser(UserRole.Moderator);
        await service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeEquivalentTo(
            new[] { TicketSubtype.UserComplaint });

        // Senior moderator: additionally complaints about junior moderator decisions
        SetCurrentUser(UserRole.SeniorModerator);
        await service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeEquivalentTo(new[]
        {
            TicketSubtype.UserComplaint,
            TicketSubtype.ModeratorDecisionComplaint
        });

        // Admin: no subtype filter at all
        SetCurrentUser(UserRole.Admin);
        await service.GetTickets(new PagingQuery());
        capturedSubtypes.Should().BeNull();
    }

    [Fact]
    public async Task ReturnEmptyWhenRequestedSubtypeIsOutOfScope()
    {
        SetCurrentUser(UserRole.Moderator);

        var (tickets, _) = await service.GetTickets(new PagingQuery(), subtype: TicketSubtype.Bug);

        tickets.Should().BeEmpty();
        await ticketRepository.DidNotReceiveWithAnyArgs().GetTickets(default!, default, default, default, default);
    }

    [Fact]
    public async Task ReturnTicketToItsReporterRegardlessOfSubtypeScope()
    {
        // A regular user (below Moderator) can always read their own ticket,
        // even one in an admin-only subtype.
        SetCurrentUser(UserRole.RegularUser);
        var ticket = new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = currentUserId,
            Subtype = TicketSubtype.Bug
        };
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await service.GetTicket(ticketId);

        result.TicketId.Should().Be(ticketId);
    }

    [Fact]
    public async Task DenyTicketToNonReporterModeratorOnAdminOnlySubtype()
    {
        // Plain moderator, not the reporter, requesting an admin-only subtype
        // by GUID: 404 (not 403) so the endpoint is not an existence oracle.
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var act = () => service.GetTicket(ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReturnTicketToInScopeModerator()
    {
        SetCurrentUser(UserRole.Moderator);
        var ticket = new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.UserComplaint
        };
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await service.GetTicket(ticketId);

        result.TicketId.Should().Be(ticketId);
    }

    [Fact]
    public async Task ReturnTicketOfAnySubtypeToAdmin()
    {
        SetCurrentUser(UserRole.Admin);
        var ticket = new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = Guid.NewGuid(),
            Subtype = TicketSubtype.Bug
        };
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await service.GetTicket(ticketId);

        result.TicketId.Should().Be(ticketId);
    }

    [Fact]
    public async Task DenyMissingTicketWithNotFound()
    {
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var act = () => service.GetTicket(ticketId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PassStatusAndSubtypeFiltersToRepositoryForMyFiledTickets()
    {
        // "Мои обращения" filters are server-side: the service forwards the
        // status and subtype straight to the repository (the reporter owns the
        // tickets, so no subtype visibility gating is applied). The page the
        // caller asked for travels with them.
        ticketRepository.GetUserTickets(
                Arg.Any<Guid>(), Arg.Any<PagingQuery>(), Arg.Any<TicketStatus?>(), Arg.Any<TicketSubtype?>(),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Ticket>(), PagingResult.Empty(20)));

        await service.GetMyFiledTickets(new PagingQuery { Skip = 40, Take = 20 },
            TicketStatus.Closed, TicketSubtype.Bug);

        await ticketRepository.Received(1).GetUserTickets(
            currentUserId, Arg.Is<PagingQuery>(q => q.Skip == 40 && q.Take == 20),
            TicketStatus.Closed, TicketSubtype.Bug, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnTicketByTrackingToken()
    {
        // Guest tracking is token-gated: possession of the token returns the
        // ticket regardless of the caller identity.
        identityProvider.Current.Returns(Identity.Guest());
        var ticket = new Ticket { TicketId = ticketId, Status = TicketStatus.WaitingForUser };
        ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(ticket);

        var result = await service.GetTicketByTrackingToken("tok123");

        result!.TicketId.Should().Be(ticketId);
    }

    [Fact]
    public async Task ReturnNullForUnknownTrackingToken()
    {
        ticketRepository.GetByTrackingToken(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Ticket?)null);

        var result = await service.GetTicketByTrackingToken("nope");

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(UserRole.RegularUser, TicketSubtype.UserComplaint)]
    [InlineData(UserRole.Moderator, TicketSubtype.AccessRecovery)]
    [InlineData(UserRole.SeniorModerator, TicketSubtype.PersonalData)]
    public async Task AnswerWhoeverMayNotReadTheTicketAsIfThereWereNone(UserRole role, TicketSubtype subtype)
    {
        SetCurrentUser(role);
        OpenTicketTakingComments(Guid.NewGuid(), subtype);

        var read = () => service.GetTicket(ticketId);
        var write = () => service.CreateComment(ticketId, new CreateTicketComment { Text = "Я тут" });

        await read.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await write.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The moderator a complaint names as the violator is an ordinary user to it:
    /// he does not read it, write in it or close it, and another moderator of his
    /// role does.
    /// </summary>
    [Fact]
    public async Task KeepAComplaintAwayFromTheModeratorItNames()
    {
        SetCurrentUser(UserRole.Moderator);
        OpenTicketTakingComments(Guid.NewGuid(), targetUserId: currentUserId);

        var read = () => service.GetTicket(ticketId);
        var write = () => service.CreateComment(ticketId, new CreateTicketComment { Text = "Это неправда" });
        var close = () => service.CreateComment(ticketId, new CreateTicketComment { Status = TicketStatus.Spam });

        await read.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await write.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await close.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
        await ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);

        OpenTicketTakingComments(Guid.NewGuid(), targetUserId: Guid.NewGuid());
        (await service.GetTicket(ticketId)).CanResolve.Should().BeTrue(
            "a complaint about somebody else is his to moderate");
    }

    /// <summary>
    /// The queue and its counters hold what the page of a ticket opens: the caller
    /// is passed down as the violator whose complaints are left out.
    /// </summary>
    [Fact]
    public async Task LeaveTheComplaintsAboutTheCallerOutOfHisQueue()
    {
        SetCurrentUser(UserRole.Moderator);
        Guid? listed = null;
        Guid? counted = null;
        QueueAnswersEmpty(violator: v => listed = v);
        ticketRepository.GetTicketCounts(
                Arg.Any<IReadOnlyCollection<TicketSubtype>?>(), Arg.Do<Guid?>(v => counted = v),
                Arg.Any<CancellationToken>())
            .Returns(new Dictionary<TicketStatus, int>());

        await service.GetTickets(new PagingQuery(), subtype: TicketSubtype.UserComplaint);
        await service.GetTicketStats();

        listed.Should().Be(currentUserId);
        counted.Should().Be(currentUserId);
    }

    private void QueueAnswersEmpty(
        Action<IReadOnlyCollection<TicketSubtype>?>? subtypes = null, Action<Guid?>? violator = null) =>
        ticketRepository.GetTickets(
                Arg.Any<PagingQuery>(),
                Arg.Any<TicketStatus?>(),
                Arg.Do<IReadOnlyCollection<TicketSubtype>?>(s => subtypes?.Invoke(s)),
                Arg.Do<Guid?>(v => violator?.Invoke(v)),
                Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Ticket>(), PagingResult.Empty(20)));
}
