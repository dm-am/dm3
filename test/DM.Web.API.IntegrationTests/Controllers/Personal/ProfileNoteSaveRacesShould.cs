using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbUserProfileNote = DM.Infrastructure.Persistence.Entities.Account.UserProfileNote;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Writes to one reader's note about one user, arriving together, are all
/// answered and leave the note as one order of them would.
/// </summary>
/// <remarks>
/// The service used to read the note of the pair and decide by what it found,
/// and that answer stops being true the moment it is given. Each test below is
/// one way it stopped being true, and each answered one of its requests with a
/// 500.
///
/// Over HTTP and against the container Postgres, because what is under test is
/// what the database decides when the writes meet. They meet only when the
/// scheduler lets them, which the test does not control, so every pair of
/// requests is played over several rounds from the same starting state, and
/// the order the two are sent in alternates between rounds so that neither
/// always gets ahead.
/// </remarks>
public class ProfileNoteSaveRacesShould : IntegrationTestBase
{
    private const int Rounds = 10;

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public ProfileNoteSaveRacesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The two ways the reader clears a note.</summary>
    public enum Clear
    {
        /// <summary>DELETE of the note.</summary>
        Delete,

        /// <summary>PUT with an empty text.</summary>
        EmptySave
    }

    /// <summary>
    /// Two saves of one note arriving together are both answered and leave one
    /// note.
    /// </summary>
    /// <remarks>
    /// A double press of "Save", or two requests of an end-to-end run: both found
    /// no note, both inserted, and the unique index over (OwnerId, SubjectUserId)
    /// refused the second insert.
    /// </remarks>
    [Fact]
    public async Task AnswerBothSavesOfOnePairArrivingTogetherAndKeepOneNote()
    {
        var owner = await SeedUser();
        var subject = await SeedUser();

        for (var round = 0; round < Rounds; round++)
        {
            await ResetNote(owner, subject, text: null);

            string[] texts = [$"первая {round}", $"вторая {round}"];

            var responses = await Task.WhenAll(texts.Select(text => Save(owner, subject.Username, text)));

            var bodies = new List<string>();
            foreach (var response in responses)
            {
                var body = await response.Content.ReadAsStringAsync();
                response.StatusCode.Should().Be(HttpStatusCode.OK,
                    "a save that met another save of the same note is still a save, round {0}: {1}",
                    round, body);
                bodies.Add(body);
            }

            var stored = await StoredNotes(owner, subject);
            stored.Should().ContainSingle("the pair holds one note however many saves arrive");
            texts.Should().Contain(stored[0].Text, "the note is one of the two saves, whichever landed last");

            var answers = bodies.Select(body => JsonSerializer.Deserialize<SavedNote>(body, Web)!).ToList();
            answers.Should().AllSatisfy(answer => answer.Id.Should().Be(stored[0].UserProfileNoteId,
                "both saves answer with the note that exists, not with an identifier nobody stored"));
            answers.Count(answer => answer.ModifiedUtc == null).Should().BeLessThanOrEqualTo(1,
                "only the save that created the note answers as a creation, the other as an update");
        }
    }

    /// <summary>
    /// A save with text and a clear of the same note arriving together are both
    /// answered, and the pair is left with the new text or with no note.
    /// </summary>
    /// <remarks>
    /// The save found the note, the clear removed it, and the save went on to
    /// update a row that was gone. Either order is a correct outcome: the clear
    /// last leaves nothing, the save last leaves its text. What is never correct
    /// is the text from before both, or a 500.
    /// </remarks>
    [Theory]
    [InlineData(Clear.Delete)]
    [InlineData(Clear.EmptySave)]
    public async Task AnswerASaveAndAClearOfOneNoteArrivingTogether(Clear clear)
    {
        var owner = await SeedUser();
        var subject = await SeedUser();

        for (var round = 0; round < Rounds; round++)
        {
            await ResetNote(owner, subject, text: "до");
            var text = $"после {round}";

            Task<HttpResponseMessage> saving;
            Task<HttpResponseMessage> clearing;
            if (round % 2 == 0)
            {
                saving = Save(owner, subject.Username, text);
                clearing = Remove(owner, subject.Username, clear);
            }
            else
            {
                clearing = Remove(owner, subject.Username, clear);
                saving = Save(owner, subject.Username, text);
            }

            await Task.WhenAll(saving, clearing);
            var saved = await saving;
            var cleared = await clearing;

            var savedBody = await saved.Content.ReadAsStringAsync();
            saved.StatusCode.Should().BeOneOf([HttpStatusCode.OK, HttpStatusCode.NoContent],
                "a save answers with the note, or with nothing when the clear landed right after it, " +
                "round {0}: {1}", round, savedBody);
            cleared.StatusCode.Should().Be(HttpStatusCode.NoContent,
                "a clear that met a save is still a clear, round {0}: {1}",
                round, await cleared.Content.ReadAsStringAsync());

            var stored = await StoredNotes(owner, subject);
            if (stored.Count == 1)
            {
                stored[0].Text.Should().Be(text, "a note that survived is the one the save wrote, round {0}", round);
            }
            else
            {
                stored.Should().BeEmpty("the pair holds at most one note");
            }

            if (saved.StatusCode == HttpStatusCode.OK)
            {
                JsonSerializer.Deserialize<SavedNote>(savedBody, Web)!.Text.Should().Be(text,
                    "a save that answers with a note answers with its own text");
            }
        }
    }

    /// <summary>
    /// Two clears of one note arriving together are both answered and leave no
    /// note.
    /// </summary>
    /// <remarks>
    /// Both loaded the note and both asked to remove it; the second removal found
    /// no row, and the change tracker took that for a lost concurrency check.
    /// </remarks>
    [Fact]
    public async Task AnswerTwoClearsOfOneNoteArrivingTogether()
    {
        var owner = await SeedUser();
        var subject = await SeedUser();

        for (var round = 0; round < Rounds; round++)
        {
            await ResetNote(owner, subject, text: "до");

            Clear[] clears = round % 2 == 0
                ? [Clear.Delete, Clear.EmptySave]
                : [Clear.EmptySave, Clear.Delete];

            var responses = await Task.WhenAll(clears.Select(clear => Remove(owner, subject.Username, clear)));

            foreach (var response in responses)
            {
                response.StatusCode.Should().Be(HttpStatusCode.NoContent,
                    "a clear of a note another clear already took is still a clear, round {0}: {1}",
                    round, await response.Content.ReadAsStringAsync());
            }

            (await StoredNotes(owner, subject)).Should().BeEmpty("both clears asked for no note");
        }
    }

    private async Task<HttpResponseMessage> Save(GeneralUser owner, string subjectUsername, string text)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Put, $"/v1/users/me/notes/{subjectUsername}", owner);
        request.Content = JsonContent.Create(new { text });
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Remove(GeneralUser owner, string subjectUsername, Clear clear)
    {
        if (clear == Clear.EmptySave)
        {
            return await Save(owner, subjectUsername, string.Empty);
        }

        var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/users/me/notes/{subjectUsername}", owner);
        return await Client.SendAsync(request);
    }

    /// <summary>
    /// Leaves the pair with exactly the note a round starts from: none, or one
    /// with the given text.
    /// </summary>
    private async Task ResetNote(GeneralUser owner, GeneralUser subject, string? text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.UserProfileNotes
            .Where(n => n.OwnerId == owner.UserId && n.SubjectUserId == subject.UserId)
            .ExecuteDeleteAsync();

        if (text == null)
        {
            return;
        }

        db.UserProfileNotes.Add(new DbUserProfileNote
        {
            UserProfileNoteId = Guid.NewGuid(),
            OwnerId = owner.UserId,
            SubjectUserId = subject.UserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<DbUserProfileNote>> StoredNotes(GeneralUser owner, GeneralUser subject)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.UserProfileNotes
            .Where(n => n.OwnerId == owner.UserId && n.SubjectUserId == subject.UserId)
            .ToListAsync();
    }

    private async Task<GeneralUser> SeedUser()
    {
        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated
        // rather than used whole.
        var username = $"pn{userId:N}"[..20];

        await using var db = DatabaseFixture.CreateDbContext();
        db.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
            LastActivityUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        return new GeneralUser
        {
            UserId = userId,
            Username = username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified
        };
    }

    private sealed record SavedNote(Guid Id, string Text, DateTimeOffset? ModifiedUtc);
}
