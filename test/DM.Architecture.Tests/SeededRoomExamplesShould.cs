using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed holds one example of every kind of room the product can build.
/// </summary>
/// <remarks>
/// A room is what is written in it and who may open it. Read from the seeder sources: the initialisers
/// hold no nested braces, held so by the count check below; one naming neither property is Default and Open.
/// </remarks>
public class SeededRoomExamplesShould
{
    /// <summary>
    /// The four rooms the game menu is read against: posts and messages, open and closed.
    /// </summary>
    private static readonly (string Type, string Access)[] Required =
    [
        ("Default", "Open"),
        ("Chat", "Open"),
        ("Default", "Private"),
        ("Chat", "Private")
    ];

    /// <summary>
    /// An entity initialiser with the name it is assigned to; a post names its room by the variable, and
    /// in another method by a parameter carrying the local's name.
    /// </summary>
    private static Regex Initialiser(string entity) => new(
        @"(?:(?:var\s+)?(?<name>\w+)\s*=\s*)?new\s+" + entity + @"\s*\{(?<body>[^{}]*)\}",
        RegexOptions.Compiled);

    private static readonly Regex RoomInitialiser = Initialiser("Room");

    private static readonly Regex PostInitialiser = Initialiser("Post");

    private static readonly Regex RoomAccessInitialiser = Initialiser("RoomAccess");

    private static readonly Regex PolicyAssignment = new(
        @"\bPolicy\s*=\s*RoomAccessPolicy\.\w+", RegexOptions.Compiled);

    /// <summary>
    /// "Type" alone: inside "AccessType" it has no word boundary before it.
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
    /// A closed room nobody wrote into shows only the lock. SeedGameRooms builds privateRoom and
    /// SeedClosedRoomScene writes through its parameter of the same name.
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
    /// A reader that parses nothing passes everything, so rooms are counted by a pattern of their own.
    /// </summary>
    [Fact]
    public void ReadEveryRoomTheSeederWrites()
    {
        var written = SeederSources()
            .Sum(source => RoomEntity.Matches(File.ReadAllText(source)).Count);

        var parsed = SeededRooms().Count;

        parsed.Should().Be(written, "every room the seeder builds has to be readable by this rule");
        parsed.Should().BeGreaterThanOrEqualTo(Required.Length, "the four rooms the menu is read against are seeded");
    }

    /// <summary>
    /// Every access row the seeder writes names its policy: the enum defaults to NoAccess.
    /// </summary>
    [Fact]
    public void GrantAPolicyOnEverySeededRoomAccess()
    {
        var rows = SeederSources()
            .SelectMany(source => RoomAccessInitialiser.Matches(File.ReadAllText(source)).Cast<Match>())
            .Select(row => row.Groups["body"].Value)
            .ToList();

        rows.Should().NotBeEmpty("the closed rooms of the seed are reached through access rows");
        rows.Should().OnlyContain(body => PolicyAssignment.IsMatch(body),
            "the enum defaults to NoAccess, and a row that names no policy grants no writing");
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
        var seeder = Path.Combine(RepositoryLayout.RootDirectory.FullName, "src", "DM.Tools.Seeder");
        Directory.Exists(seeder).Should().BeTrue("the seeder is the tool that builds the fixture");
        return RepositoryFiles.Files(seeder, "*.cs", BuildOutput).ToList();
    }


    private sealed record SeededRoom(string? Name, string Type, string Access);
}
