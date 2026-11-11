using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// A catch-all decides for every failure at once, the ones nobody thought of included, so this test is
/// the register. Counted: <c>catch (Exception</c> with or without a variable or a filter, and a bare
/// <c>catch</c>. Left out: generated migrations and the test tree. A record is keyed by file and the
/// method the handler stands in, not by line, so an edit above it does not move the record.
/// </remarks>
public class CatchAllRegisterShould
{
    private sealed record CatchAll(string File, string Method, string Why);

    private static readonly CatchAll[] Allowed =
    [
        // Domain

        new("src/DM.Domain.Account/Features/Authentication/LoginRecordService.cs", "RecordAttempt",
            "A failed write of the sign-in history must not fail the sign-in itself; the failure is a " +
            "warning with the exception, and the security journal is written separately."),

        new("src/DM.Domain.Account/Features/Authentication/SessionToken.cs", "Read",
            "Already filtered to JsonException, FormatException and CryptographicException, what a " +
            "forged or damaged token throws: an unreadable token is no token."),

        new("src/DM.Domain.Account/Features/Security/HibpPasswordChecker.cs", "IsCompromisedAsync",
            "Fail-open on purpose: an outage of the outside breach service must not stop registration " +
            "or a password change. The skip is a warning in the log, and the wait is capped."),

        new("src/DM.Domain.Core/Extensions/ReadableGuidHelper.cs", "TryDecodeFromReadableGuid",
            "Already filtered to FormatException and ArgumentException, the two a malformed readable " +
            "identifier raises, so a try-form answers false instead of throwing."),

        new("src/DM.Domain.Core/UnreadCounters/UnreadCountersExtensions.cs", "ReserveAsync",
            "Cleanup and rethrow: whatever the reservation write throws, the reservation is disposed " +
            "and the same exception goes on."),

        new("src/DM.Domain.Core/UnreadCounters/UnreadCountersReservation.cs", "DisposeAsync",
            "A failure of the rollback delete is swallowed because the caller's exception, already in " +
            "flight, outranks it; reporting the second would hide the first."),

        new("src/DM.Domain.Forum/Features/Digests/PeriodDigestProcessor.cs", "CreateDigestTopic",
            "Cleanup and rethrow: whatever topic creation throws, cancellation included, the claimed " +
            "period is released so the next pass can retry it, then the failure goes on."),

        new("src/DM.Domain.Forum/Features/Digests/PeriodDigestProcessor.cs", "CreateDigestTopic",
            "Inside the release above: a failure of the release is logged so that a second failure " +
            "does not mask the creation failure being rethrown."),

        new("src/DM.Domain.Game/Features/Games/GameService.cs", "InviteAssistantOnCreation",
            "The game is committed by the time the assistant is invited, so a refused or failed " +
            "invitation must not turn a created game into an error; it is logged. Telling the master " +
            "is a visible change waiting in the catalogue."),

        new("src/DM.Domain.Game/Features/Inactivity/GameInactivityProcessor.cs", "ProcessAsync",
            "One game's database or producer failure must not stop the pass for every other game: " +
            "it is logged and the pass goes on to the next one."),

        new("src/DM.Domain.Personal/Features/Notifications/NotificationWording.cs", "ReadFields",
            "Already filtered to the serializer's own failures: a payload bag that cannot be read as " +
            "fields costs the notification its details, not its delivery."),

        // Infrastructure

        new("src/DM.Infrastructure.Core/Geo/CountryTableRefresher.cs", "ExecuteAsync",
            "Already filtered to IOException and UnauthorizedAccessException: an unreadable table " +
            "file is treated as no file, and a fresh download follows."),

        new("src/DM.Infrastructure.Core/Geo/CountryTableRefresher.cs", "RefreshAsync",
            "HTTP, IO, data and timeout failures of the download keep the table in use and schedule a " +
            "retry: the host must not stop over a lookup table, and cancellation is rethrown above."),

        new("src/DM.Infrastructure.Core/Geo/CountryTableRefresher.cs", "Discard",
            "Already filtered to IOException and UnauthorizedAccessException: a leftover partial " +
            "download that cannot be deleted is only logged, the next refresh overwrites it."),

        new("src/DM.Infrastructure.Core/Paging/CursorService.cs", "Decode",
            "Already filtered to FormatException and JsonException: a damaged cursor reads as the " +
            "first page, and refusing it with 400 would be a visible change."),

        new("src/DM.Infrastructure.Core/Storage/ImageProcessingService.cs", "Decode",
            "Already filtered to the two Magick resource exceptions, which mean the image is too " +
            "large to decode and are answered as the TooManyPixels refusal."),

        new("src/DM.Infrastructure.Core/Storage/ObjectStorage.cs", "DeleteAsync",
            "Callers delete best-effort and delete again by design, so a storage failure of a delete " +
            "is false rather than an error; cancellation is rethrown above and a missing object is true."),

        new("src/DM.Infrastructure.Messaging/Queues/QueueWorker.cs", "ExecuteAsync",
            "A loop error, the claim, the mark or the connection failing, is logged and the loop " +
            "waits on its ladder: an exception leaving the loop would stop the whole host."),

        new("src/DM.Infrastructure.Messaging/Queues/QueueWorker.cs", "Handle",
            "Filtered to a stop of the host: whatever the handler throws on its way out is turned " +
            "into the cancellation, so the claim rolls back and the row spends no attempt."),

        new("src/DM.Infrastructure.Messaging/Queues/QueueWorker.cs", "Handle",
            "A failure of the handler is logged and written on the row as an attempt with its last " +
            "error, so the row climbs the retry ladder; a stop of the host is taken by the filter above."),

        new("src/DM.Infrastructure.Persistence/RelationalStorage/ExpressionIndexInitializer.cs", "Assert",
            "Fail-open on purpose: when the case-insensitive unique indexes cannot be asserted at start, " +
            "the site still serves and the failure is an error in the log. The application check still " +
            "refuses a taken name or address."),

        new("src/DM.Infrastructure.Persistence/RelationalStorage/OutboxEventProducer.cs", "SendAsync",
            "A refused outbox save is detached, counted and logged so that the business " +
            "write that raised the event stands."),

        new("src/DM.Infrastructure.Persistence/Repositories/Account/TwoFactorRepository.cs", "SaveOrClearTracker",
            "Cleanup and rethrow: any save failure clears the change tracker so the context is usable " +
            "again, and the same exception goes on."),

        new("src/DM.Infrastructure.Persistence/Repositories/General/UploadRepository.cs", "AddAsync",
            "Already filtered to DbUpdateException and NpgsqlException, wrapped into StorageException " +
            "with the cause kept."),

        new("src/DM.Infrastructure.Persistence/Repositories/Personal/NotificationRepository.cs", "Create",
            "Cleanup and rethrow: any save failure clears the tracker so the retry on the same context " +
            "does not collide on the (EventId, EventType) index, and the same exception goes on."),

        new("src/DM.Infrastructure.Persistence/Repositories/Search/SearchTextProjection.cs", "Of",
            "Any parser or renderer failure falls back to the text cut before the first private block, " +
            "so the body stays searchable and the private block never reaches the index."),

        new("src/DM.Infrastructure.Persistence/RelationalStorage/QueueMetrics.cs", "Read",
            "A scrape callback: whatever the reading throws, every queue reads not-a-number, so an " +
            "outage shows as unknown and not as an empty queue, and the scrape still answers. The " +
            "failure is logged at Warning when it starts and the recovery at Information."),

        new("src/DM.Infrastructure.Persistence/Shared/UnreadCounters/UnreadCountersRepository.cs", "CountedAsync",
            "The counter write follows work that is already committed, so throwing would fail a request " +
            "whose content is saved; the loss is counted in WriteLost and logged."),

        // Hosts

        new("src/DM.Tools.Seeder/Program.cs", "Main",
            "The top-level boundary of a console tool: any failure is printed to stderr and becomes " +
            "exit code 1 instead of an unhandled crash."),

        new("src/DM.Web.API/Features/Account/Registration/RegistrationController.cs", "Activate",
            "Not a catch-all in effect: the DisclosesTaken filter lets through only the refusal that " +
            "names a taken username, which is logged as a disclosure and rethrown."),

        new("src/DM.Web.API/Features/General/Upload/UploadApiService.DirectUpload.cs", "DirectUploadInstrumented",
            "Count and rethrow: whatever the specific handlers above do not name is counted under the " +
            "reason other, and the same exception goes on."),

        new("src/DM.Web.API/Features/General/Upload/UploadApiService.DirectUpload.cs", "DirectUploadCore",
            "Cleanup and rethrow: a storage or database failure of the save deletes the thumbnails " +
            "already written, so none is left without its row, and the same exception goes on."),

        new("src/DM.Web.API/Features/Personal/Webhooks/WebhookController.cs", "HandleWebhook",
            "A failure while handling an update that passed the checks is logged as an error and " +
            "answered with 200, so that Telegram does not redeliver the same failing update in a loop."),

        new("src/DM.Web.API/HostedServices/PeriodicHostedService.cs", "Guarded",
            "Cancellation is excluded by the filter; any other failure of one step is logged and the " +
            "next pass retries, so one bad pass does not stop the job for good."),

        new("src/DM.Web.API/HostedServices/WarmupService.cs", "StartAsync",
            "Warmup is not critical: a database or timeout failure of its queries only delays the " +
            "first request, so it is logged and startup goes on."),

        new("src/DM.Web.API/Middleware/ErrorHandlingMiddleware.cs", "InvokeAsync",
            "The one top-level handler of the API: every exception that reaches it is mapped to the " +
            "error response, which is its whole job."),

        new("src/DM.Web.API/Notifications/RealtimeAvatarBroadcaster.cs", "BroadcastAvatarChangedAsync",
            "The avatar change is already saved and the push is best-effort, so a SignalR or lookup " +
            "failure is logged rather than failing the change."),

        new("src/DM.Web.API/Realtime/PushListener.cs", "ListenAsync",
            "Whatever the listener's connection throws, it is logged and the listener reconnects on " +
            "its ladder: an exception out of a hosted service stops the host, and the health check " +
            "reports push as off meanwhile."),

        new("src/DM.Web.API/Realtime/PushListener.cs", "DeliverOneAsync",
            "One push the hub refused is counted on the delivery-failure counter and logged, so the " +
            "pushes behind it in the buffer still reach their tabs."),

        new("src/DM.Web.API/Shared/Authentication/TwoFactorChallengeCookie.cs", "Read",
            "Already filtered to FormatException and CryptographicException, what decryption throws on " +
            "a forged or malformed cookie: such a cookie reads as no challenge."),

        new("src/DM.Web.API/Shared/Authentication/WebAuthenticationService.cs", "ReportSuspiciousLogin",
            "The sign-in is already done when the suspicious-login check runs, and a failure of the " +
            "detector or of its journal write may not undo it; it is logged."),

        new("src/DM.Web.API/Shared/Authentication/WebAuthenticationService.cs", "SendSuspiciousLoginLetter",
            "The journal entry is written and the sign-in stands, so a letter that cannot be sent is " +
            "counted on the PublishFailed counter and logged rather than failing the sign-in."),

        // Workers

        new("src/DM.Workers.Mail/Sending/MailSendingProcessor.cs", "DisposeAsync",
            "The letter is already sent when the session closes, so a failure to quit the " +
            "SMTP session cleanly is logged rather than failing the message."),

        new("src/DM.Workers.Mail/Sending/MailSendingProcessor.cs", "Dispose",
            "The synchronous twin of DisposeAsync: the letter is already sent, so a failure " +
            "to quit the SMTP session is logged rather than failing the message."),

        new("src/DM.Workers.NotificationDispatcher/Bot/NotificationBotSender.cs", "SendDiscordMessage",
            "A failed delivery to the bot API answers false, so the caller counts it and the other " +
            "recipients and channels still get theirs."),

        new("src/DM.Workers.NotificationDispatcher/Bot/NotificationBotSender.cs", "SendTelegramMessage",
            "A failed delivery to the bot API answers false, so the caller counts it and the other " +
            "recipients and channels still get theirs."),

        new("src/DM.Workers.NotificationDispatcher/Dispatching/NotificationProcessor.cs", "Deliver",
            "One channel's failure is counted as DeliveryFailed and logged so that the other " +
            "channels of the same notification still run."),

        new("src/DM.Workers.NotificationDispatcher/Email/NotificationEmailSender.cs", "SendIfEnabled",
            "The email is one channel of a notification already stored on the site: a failure " +
            "is counted as DeliveryFailed and logged, and that letter is lost rather than redelivered."),
    ];

    private static readonly Regex Handler = new(
        @"\bcatch\s*(?:\(\s*(?:System\.)?Exception\b|\{|when\b|$)", RegexOptions.Compiled);

    /// <summary>
    /// Nothing catches everything that is not listed above, and nothing is listed
    /// that no longer does.
    /// </summary>
    /// <remarks>Checked from both ends so the list cannot rot into a licence.</remarks>
    [Fact]
    public void CarryAReasonForEveryHandlerThatCatchesEverything()
    {
        var found = Count(Scan());
        var listed = Count(Allowed.Select(entry => Key(entry.File, entry.Method)));

        var undeclared = found
            .Where(pair => pair.Value > listed.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} found, {listed.GetValueOrDefault(pair.Key)} listed")
            .ToArray();
        var stale = listed
            .Where(pair => pair.Value > found.GetValueOrDefault(pair.Key))
            .Select(pair => $"{pair.Key}: {pair.Value} listed, {found.GetValueOrDefault(pair.Key)} found")
            .ToArray();

        string.Join(Environment.NewLine, undeclared).Should().BeEmpty(
            "a handler that catches everything without a written reason swallows failures " +
            "nobody decided to swallow: add it to Allowed with the reason, or catch what the " +
            "code inside it actually throws");
        string.Join(Environment.NewLine, stale).Should().BeEmpty(
            "the handler is gone or narrowed, so the reason for it is spent: drop the entry " +
            "rather than leave it standing for the next one to be added under");
    }

    [Fact]
    public void GiveEveryRecordAReason()
    {
        Allowed.Where(entry => string.IsNullOrWhiteSpace(entry.Why)).Should().BeEmpty(
            "a record without a reason is a licence");
        Allowed.Where(entry => !File.Exists(Path.Combine(DM.Testing.RepositoryLayout.Root, entry.File)))
            .Select(entry => entry.File).Should().BeEmpty("a record names a file of the tree");
    }

    private static string Key(string file, string method) => $"{file} {method}";

    private static Dictionary<string, int> Count(IEnumerable<string> keys) => keys
        .GroupBy(key => key, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static IEnumerable<string> Scan()
    {
        foreach (var path in RepositoryFiles.ProductionSources())
        {
            var file = HandwrittenDocuments.Relative(path);
            if (file.Contains("/Migrations/", StringComparison.Ordinal)) continue;

            var text = File.ReadAllText(path);
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
            IReadOnlyList<SourceText.Span>? spans = null;
            for (var index = 0; index < lines.Length; index++)
            {
                if (!Handler.IsMatch(CodeOf(lines[index]))) continue;

                spans ??= SourceText.MemberSpans(text);
                var number = index + 1;
                var method = spans
                    .Where(span => span.Line <= number && number < span.Line + span.Lines)
                    .OrderBy(span => span.Lines)
                    .Select(span => span.Name)
                    .FirstOrDefault() ?? "(outside a method)";
                yield return Key(file, method);
            }
        }
    }

    private static string CodeOf(string line)
    {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return (comment < 0 ? line : line[..comment]).TrimEnd();
    }
}
