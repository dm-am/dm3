using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed holds one example of every kind of room the product can build.
/// </summary>
/// <remarks>
/// A room is two independent choices: what is written in it (posts or messages)
/// and who may open it (anyone, or the characters that were granted access). The
/// seed used to write three of those four rooms and no closed chat room at all,
/// and the closed post room it did write stayed empty, so the screen that shows
/// the difference — the game menu, where a closed room carries a lock — could
/// not be looked at in the state that matters. The lock on it was broken for
/// exactly as long as nothing in the seed wore it.
///
/// The rule reads the seeder sources rather than a seeded database: seeding needs
/// PostgreSQL, MongoDB and object storage, and a fixture whose whole purpose is
/// to be looked at is defended by asserting that it is still written. The room
/// and post initialisers hold no nested braces, which is what makes reading them
/// out of the text sound rather than clever, and the reader is held to that by
/// its own count check below.
///
/// An initialiser that names neither property counts as Default and Open: that is
/// what the enum values and the column defaults give it.
/// </remarks>
public class SeededRoomExamplesShould
{
    /// <summary>
    /// The four rooms the game menu is read against. Posts and messages, open and
    /// closed: dropping any one of them takes a state off the screen.
    /// </summary>
    private static readonly (string Type, string Access)[] Required =
    [
        ("Default", "Open"),
        ("Chat", "Open"),
        ("Default", "Private"),
        ("Chat", "Private")
    ];

    /// <summary>
    /// An entity initialiser together with the local it is assigned to, so that a
    /// room can be matched with the posts written into it. A room the seeder builds
    /// is always assigned to a local first, which is what makes the name a handle
    /// on it.
    /// </summary>
    private static Regex Initialiser(string entity) => new(
        @"(?:(?:var\s+)?(?<name>\w+)\s*=\s*)?new\s+" + entity + @"\s*\{(?<body>[^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex RoomInitialiser = Initialiser("Room");

    private static readonly Regex PostInitialiser = Initialiser("Post");

    /// <summary>
    /// "Type" alone: the word inside "AccessType" is not preceded by a word
    /// boundary, so the two assignments cannot be read for each other.
    /// </summary>
    private static readonly Regex TypeAssignment = new(
        @"\bType\s*=\s*RoomType\.(?<value>\w+)", RegexOptions.Compiled);

    private static readonly Regex AccessAssignment = new(
        @"\bAccessType\s*=\s*RoomAccessType\.(?<value>\w+)", RegexOptions.Compiled);

    private static readonly Regex RoomReference = new(
        @"\bRoomId\s*=\s*(?<name>\w+)\.RoomId", RegexOptions.Compiled);

    private static readonly Regex RoomEntity = new(@"new\s+Room\b", RegexOptions.Compiled);

    private static readonly string[] BuildOutput = ["bin", "obj"];

    [Fact]
    public void HoldEveryPairOfTypeAndAccess()
    {
        var seeded = SeededRooms();

        var missing = Required
            .Where(room => !seeded.Any(seed => seed.Type == room.Type && seed.Access == room.Access))
            .Select(room => room.Type + "/" + room.Access)
            .ToList();

        missing.Should().BeEmpty(
            "the game menu is the screen where a closed room differs from an open one, " +
            "and it can only be looked at against a seed holding all four rooms: " +
            "posts and messages, open and closed");
    }

    /// <summary>
    /// A closed room that nobody wrote into is a lock over an empty page, which
    /// shows the lock and nothing else. The seeded rooms are locals, so the posts
    /// that name one are the posts written into it. The seeder also fetches rooms
    /// into locals of its own, so two files would have to agree on a name for this
    /// to read one room for another.
    /// </summary>
    [Fact]
    public void WriteIntoTheClosedPostRoom()
    {
        var closed = SeededRooms()
            .Where(room => room is { Type: "Default", Access: "Private", Name: not null })
            .Select(room => room.Name!)
            .ToList();

        closed.Should().NotBeEmpty("the closed post room is one of the four the menu is read against");

        var written = closed.Intersect(PostedRooms(), StringComparer.Ordinal).ToList();

        written.Should().NotBeEmpty(
            "a closed room is an example of a closed room only once something stands behind the lock, " +
            "and the private room of the seeded game carried no posts at all");
    }

    /// <summary>
    /// A reader that parses nothing passes everything. The count is taken from a
    /// pattern that shares nothing with the reader beyond the entity name, so a
    /// room written in a shape the reader cannot take apart fails here instead of
    /// quietly leaving the rule above with less to check.
    /// </summary>
    [Fact]
    public void ReadEveryRoomTheSeederWrites()
    {
        var written = SeederSources()
            .Sum(source => RoomEntity.Matches(File.ReadAllText(source)).Count);

        var parsed = SeededRooms().Count;

        parsed.Should().Be(written, "every room the seeder builds has to be readable by this rule");
        parsed.Should().BeGreaterOrEqualTo(Required.Length, "the four rooms the menu is read against are seeded");
    }

    /// <summary>The rooms the seeder builds, in the order they are written.</summary>
    private static IReadOnlyList<SeededRoom> SeededRooms()
    {
        var rooms = new List<SeededRoom>();

        foreach (var source in SeederSources())
        {
            var text = File.ReadAllText(source);
            foreach (Match initialiser in RoomInitialiser.Matches(text))
            {
                var body = initialiser.Groups["body"].Value;
                rooms.Add(new SeededRoom(
                    initialiser.Groups["name"].Success ? initialiser.Groups["name"].Value : null,
                    Assigned(TypeAssignment, body, "Default"),
                    Assigned(AccessAssignment, body, "Open")));
            }
        }

        return rooms;
    }

    /// <summary>The names of the rooms the seeder writes posts into.</summary>
    private static IReadOnlyCollection<string> PostedRooms()
    {
        var rooms = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in SeederSources())
        {
            var text = File.ReadAllText(source);
            foreach (Match initialiser in PostInitialiser.Matches(text))
            {
                var room = RoomReference.Match(initialiser.Groups["body"].Value);
                if (room.Success)
                {
                    rooms.Add(room.Groups["name"].Value);
                }
            }
        }

        return rooms;
    }

    private static string Assigned(Regex assignment, string body, string whenAbsent)
    {
        var match = assignment.Match(body);
        return match.Success ? match.Groups["value"].Value : whenAbsent;
    }

    private static IReadOnlyList<string> SeederSources()
    {
        var seeder = Path.Combine(RepositoryRoot.FullName, "src", "DM.Tools.Seeder");
        Directory.Exists(seeder).Should().BeTrue("the seeder is the tool that builds the fixture");
        return SourceFiles(seeder).ToList();
    }

    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs"))
        {
            yield return file;
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (BuildOutput.Contains(Path.GetFileName(nested)))
            {
                continue;
            }

            foreach (var file in SourceFiles(nested))
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static DirectoryInfo RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docs")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!;
        }
    }

    private sealed record SeededRoom(string? Name, string Type, string Access);
}
