using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DM.Workers.Mail;

/// <summary>
/// The loop of the mail worker over the mail queue, the same loop the notification worker runs.
/// </summary>
/// <remarks>
/// A refusing mail server is a failure: the letter climbs the retry ladder and goes out on the
/// first attempt after the server is back, with no human action. The sent letter's body is
/// erased by the command that marks it processed.
/// </remarks>
internal static class MailQueue
{
    /// <summary>Builds the loop from the services of the process.</summary>
    public static QueueWorker Worker(IServiceProvider services) => new(
        QueueNames.Mail,
        null,
        Handle,
        services.GetRequiredService<NpgsqlDataSource>(),
        services.GetRequiredService<IServiceScopeFactory>(),
        services.GetRequiredService<IDateTimeProvider>(),
        services.GetRequiredService<ILogger<QueueWorker>>());

    /// <summary>
    /// Hands the letter of a row to the unchanged letter processor, under the queue's name where
    /// the routing key used to be.
    /// </summary>
    /// <remarks>
    /// Text that is not a letter stays so on every pass: the answer is that it cannot be processed,
    /// which makes the row dead at once and lets the rows behind it go on.
    /// </remarks>
    public static Task<ProcessResult> Handle(OutboxRow row, IServiceProvider scope, CancellationToken cancellationToken) =>
        LetterPayload.TryRead(row.Payload, out var letter)
            ? scope.GetRequiredService<IProcessor<string, EmailLetter>>().Process(QueueNames.Mail, letter, cancellationToken)
            : Task.FromResult(ProcessResult.Failure);
}
