using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Mail;
using DM.Testing;
using FluentValidation;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

/// <summary>
/// The mail sender stores a letter as an outbox row of the mail queue, and a letter it could not
/// store is the caller's error: the letter is the whole of what the request owes.
/// </summary>
/// <remarks>
/// Over a source on a closed port, so the insert fails without a database. The insert that
/// succeeds is proven over a real database in DM.Workers.Mail.Tests (MailQueueShould).
/// </remarks>
public class MailSenderShould : UnitTestBase
{
    private static readonly EmailLetter Letter = new()
    {
        Address = "player@example.com",
        Subject = "Confirm your registration",
        Body = "<p>Hello</p>",
    };

    /// <remarks>Mutation it must fail on: an insert whose refusal is logged and swallowed, as an event's is.</remarks>
    [Fact]
    public async Task ThrowToTheCallerWhenTheLetterCannotBeStored()
    {
        await using var unreachable = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=dm3;Username=dm_app;Password=x;Timeout=2");
        var sender = Sender(unreachable);

        var sending = () => sender.SendAsync(Letter);

        await sending.Should().ThrowAsync<NpgsqlException>(
            "a request whose letter was not stored has to answer with an error, so the person " +
            "tries again instead of waiting for a letter that will not come");
    }

    [Fact]
    public async Task RefuseAnInvalidLetterBeforeTouchingTheDatabase()
    {
        await using var unreachable = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Database=dm3;Username=dm_app;Password=x;Timeout=2");
        var sender = Sender(unreachable);

        var sending = () => sender.SendAsync(new EmailLetter { Address = "nobody", Subject = "s", Body = "b" });

        await sending.Should().ThrowAsync<ValidationException>();
    }

    private static MailSender Sender(NpgsqlDataSource dataSource)
    {
        var guids = Substitute.For<IGuidFactory>();
        guids.Create().Returns(Guid.NewGuid());
        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        return new MailSender(new EmailLetterValidator(), dataSource, guids, clock);
    }
}
