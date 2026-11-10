using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Queues;
using FluentValidation;
using Npgsql;
using NpgsqlTypes;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Stores a letter as an outbox row of the mail queue, for the mail worker to claim.
/// </summary>
/// <remarks>
/// One command through the process's connection source and not the request's change tracker: the
/// caller stores the letter after its own write has committed, and a flush of the tracker here
/// would commit whatever else the request left in it. A refused insert is thrown, because the
/// letter is the whole of what the request owes and is recoverable from nothing else.
/// </remarks>
internal class MailSender(
    IValidator<EmailLetter> validator,
    NpgsqlDataSource dataSource,
    IGuidFactory guidFactory,
    IDateTimeProvider dateTimeProvider) : IMailSender
{
    /// <inheritdoc />
    public async Task SendAsync(EmailLetter letter)
    {
        await validator.ValidateAndThrowAsync(letter);

        await using var command = dataSource.CreateCommand(
            """
            INSERT INTO "OutboxEvents" ("EventId", "Queue", "Payload", "TraceParent", "OccurredUtc")
            VALUES ($1, $2, $3, $4, $5)
            """);
        command.Parameters.Add(new NpgsqlParameter { Value = guidFactory.Create() });
        command.Parameters.Add(new NpgsqlParameter { Value = QueueNames.Mail });
        command.Parameters.Add(new NpgsqlParameter { Value = LetterPayload.Write(letter) });
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = (object?)StoredTraceParent.Current() ?? DBNull.Value,
            NpgsqlDbType = NpgsqlDbType.Text,
        });
        command.Parameters.Add(new NpgsqlParameter { Value = dateTimeProvider.Now.ToUniversalTime() });

        // Not the request's token: a letter half stored is a letter lost.
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
}
