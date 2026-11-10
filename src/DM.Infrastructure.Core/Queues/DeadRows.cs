using System.Globalization;

namespace DM.Infrastructure.Core.Queues;

/// <summary>
/// How a human returns a dead row: one database command, no deployment and no screen.
/// </summary>
/// <remarks>
/// The command clears the death, the attempts and the not-before moment, so the row is ready at
/// once and gets the whole retry ladder again, and it touches only a row that is dead and not
/// processed. The last error stays as the trace of why the row died. An account letter dead for
/// more than 48 hours is deleted instead: its link has expired.
/// </remarks>
public static class DeadRows
{
    /// <summary>The command that returns the dead row of the given Id, as an operator runs it in psql.</summary>
    /// <param name="id">The Id of the dead row.</param>
    public static string ReturnCommand(long id) =>
        $"""UPDATE "OutboxEvents" SET "DeadUtc" = NULL, "Attempts" = 0, "NextAttemptUtc" = NULL WHERE "Id" = {id.ToString(CultureInfo.InvariantCulture)} AND "DeadUtc" IS NOT NULL AND "ProcessedUtc" IS NULL;""";
}
