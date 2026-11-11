using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using DM.Infrastructure.Core.Queues;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// A letter of a request is an outbox row of the mail queue, and a letter that could not be stored
/// is an error of the request.
/// </summary>
/// <remarks>
/// The refusal is a trigger that rejects the rows carrying this test's address alone, so no other
/// test of the shared database loses a letter.
/// </remarks>
public class MailOutboxShould : IntegrationTestBase
{
    public MailOutboxShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <remarks>Mutation it must fail on: a letter stored without the trace parent of the request.</remarks>
    [Fact]
    public async Task KeepTheRegistrationLetterInTheMailQueueWithTheTraceOfTheRequest()
    {
        var email = $"queued{Guid.NewGuid():N}"[..20] + "@test.example.com";

        var register = await Client.PostAsJsonAsync("/v1/account/register",
            new { email, password = TestConstants.Password, acceptedRules = true });

        register.StatusCode.Should().Be(HttpStatusCode.Created, await register.Content.ReadAsStringAsync());
        await using var db = DatabaseFixture.CreateDbContext();
        var row = await db.OutboxEvents.AsNoTracking()
            .SingleAsync(r => r.Queue == QueueNames.Mail && r.Payload!.Contains(email));
        row.EventType.Should().BeNull("a letter is no event");
        row.EntityId.Should().BeNull();
        row.TraceParent.Should().MatchRegex("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$",
            "the mail worker's span continues the trace of the registration request");
    }

    /// <remarks>Mutation it must fail on: a mail sender that logs and swallows a refused insert.</remarks>
    [Fact]
    public async Task AnswerWithAnErrorWhenTheLetterCannotBeStored()
    {
        var email = $"refused{Guid.NewGuid():N}"[..20] + "@test.example.com";
        var trigger = $"refuse_{Guid.NewGuid():N}";
        await Run($"""
                   CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$
                   BEGIN RAISE EXCEPTION 'the queue refuses this letter'; END $$;
                   CREATE TRIGGER {trigger} BEFORE INSERT ON "OutboxEvents" FOR EACH ROW
                   WHEN (NEW."Queue" = '{QueueNames.Mail}' AND NEW."Payload" LIKE '%{email}%')
                   EXECUTE FUNCTION {trigger}();
                   """);
        try
        {
            var register = await Client.PostAsJsonAsync("/v1/account/register",
                new { email, password = TestConstants.Password, acceptedRules = true });

            register.StatusCode.Should().Be(HttpStatusCode.InternalServerError,
                "a person whose letter will not come has to learn it now and try again");
        }
        finally
        {
            await Run($"""
                       DROP TRIGGER {trigger} ON "OutboxEvents";
                       DROP FUNCTION {trigger}();
                       """);
        }
    }

    private async Task Run(string sql)
    {
        await using var connection = new NpgsqlConnection(DatabaseFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
