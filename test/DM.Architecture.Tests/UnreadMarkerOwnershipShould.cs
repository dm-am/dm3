using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An unread marker is written before the relational row it belongs to, and by the
/// reservation that can take it back.
/// </summary>
/// <remarks>
/// There is no transaction across the two stores and no outbox, so a feature living
/// in both owes an explicit order — DATA_STORAGE.md states it, and the code held it
/// in one of the six places that needed it. The other five wrote the marker after
/// the insert, where a refusal from the document store leaves a committed entity
/// whose counters do not exist and never will: nothing recreates them, and its badge
/// reads zero for everybody forever.
///
/// None of that is visible in a green build or in a running stand. Both stores have
/// to be up for the code to work at all, and the failure only appears when one of
/// them refuses at the exact moment between the two writes. Text is the only surface
/// there is, and it is read with the comments taken out — a paragraph explaining the
/// order contains every word this rule looks for.
/// </remarks>
public class UnreadMarkerOwnershipShould
{
    private static string SourceDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "src");

    /// <summary>The two places allowed to write a marker: the contract and its one implementation.</summary>
    private static readonly string[] Owners =
    [
        Path.Combine("DM.Domain.Core", "UnreadCounters"),
        Path.Combine("DM.Infrastructure.Persistence", "Shared", "UnreadCounters"),
    ];

    /// <summary>
    /// Every caller, with the relational write its markers stand in front of. Named
    /// rather than discovered: what makes a line the row of an entity is what it
    /// means, and the whole point is that the two orders are indistinguishable to
    /// anything but a reader.
    /// </summary>
    private static readonly (string File, string[] RelationalWrites)[] Callers =
    [
        (Path.Combine("DM.Domain.Forum", "Features", "Topics", "TopicService.cs"), ["_repository.Create("]),
        (Path.Combine("DM.Domain.Forum", "Features", "Digests", "PeriodDigestProcessor.cs"), ["_topicRepository.Create("]),
        (Path.Combine("DM.Domain.Game", "Features", "Games", "GameService.cs"), ["_repository.Create("]),
        (Path.Combine("DM.Domain.Game", "Features", "Rooms", "RoomService.cs"), ["_repository.Create("]),
        (Path.Combine("DM.Domain.Blog", "Features", "Publications", "PublicationService.cs"), ["_repository.CreatePublication("]),
        (Path.Combine("DM.Domain.Messaging", "Features", "Chats", "ChatService.cs"),
            ["_repository.Create(chat, chatLinks)", "_repository.Update(updateEntity)"]),
    ];

    private static bool IsAuthored(string path) =>
        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
        !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static IEnumerable<(string Path, string Code)> Sources() => Directory
        .EnumerateFiles(SourceDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .Select(path => (path, SourceText.ReadCode(path)));

    private static bool IsOwner(string path) =>
        Owners.Any(owner => path.Contains(owner, StringComparison.Ordinal));

    /// <summary>
    /// A rule that found nothing passes, and both halves of this one are searches.
    /// </summary>
    [Fact]
    public void FindTheIdiomInTheTree()
    {
        var sources = Sources().ToList();

        sources.Count(source => source.Code.Contains("ReserveAsync(", StringComparison.Ordinal))
            .Should().BeGreaterThanOrEqualTo(6, "every entity with unread counters reserves them");
        sources.Count(source => source.Code.Contains("CreateMarkerAsync(", StringComparison.Ordinal))
            .Should().BeGreaterThanOrEqualTo(2, "the contract declares the write and one class implements it");
    }

    [Fact]
    public void WriteNoMarkerOutsideTheReservation()
    {
        var offenders = Sources()
            .Where(source => !IsOwner(source.Path))
            .Where(source => source.Code.Contains("CreateMarkerAsync(", StringComparison.Ordinal))
            .Select(source => Path.GetFileName(source.Path))
            .ToList();

        offenders.Should().BeEmpty(
            "a marker written outside a reservation is one nothing can take back, and the " +
            "entity it belongs to may still fail to land");
    }

    [Fact]
    public void BindEveryReservationAndCommitIt()
    {
        foreach (var (path, code) in Sources().Where(source => !IsOwner(source.Path)))
        {
            // The statement rather than the line: what has to carry the binding is
            // the declaration, and it is free to wrap.
            foreach (Match match in Regex.Matches(code, @"ReserveAsync\("))
            {
                var statement = code.LastIndexOfAny([';', '{', '}'], match.Index) + 1;
                code[statement..match.Index].Should().Contain("await using",
                    $"{Path.GetFileName(path)} holds a reservation nothing disposes, so a refused " +
                    "insert leaves its markers behind");
            }

            if (code.Contains("ReserveAsync(", StringComparison.Ordinal))
            {
                code.Should().Contain(".Commit()",
                    $"{Path.GetFileName(path)} reserves markers and never commits them, so every " +
                    "successful write takes its own counters away again");
            }
        }
    }

    [Fact]
    public void ReserveBeforeTheRowItProtects()
    {
        foreach (var (file, relationalWrites) in Callers)
        {
            var code = SourceText.ReadCode(Path.Combine(SourceDirectory, file));
            var reserved = code.IndexOf("ReserveAsync(", StringComparison.Ordinal);

            reserved.Should().BeGreaterThan(-1, $"{file} writes unread markers");

            foreach (var write in relationalWrites)
            {
                var inserted = code.IndexOf(write, StringComparison.Ordinal);
                inserted.Should().BeGreaterThan(-1, $"{file} is expected to call {write}");
                inserted.Should().BeGreaterThan(reserved,
                    $"{file} inserts the row before reserving its markers, and a refusal from the " +
                    "document store then leaves an entity whose counters never exist");
            }
        }
    }

    /// <summary>
    /// Coverage from the other end: a seventh caller would otherwise slip past the
    /// order check above by not being on its list.
    /// </summary>
    [Fact]
    public void NameEveryCallerTheTreeHas()
    {
        var reserving = Sources()
            .Where(source => !IsOwner(source.Path))
            .Where(source => source.Code.Contains("ReserveAsync(", StringComparison.Ordinal))
            .Select(source => Path.GetRelativePath(SourceDirectory, source.Path))
            .ToList();

        reserving.Should().BeEquivalentTo(Callers.Select(caller => caller.File),
            "the order of two lines is what this class is about, and a caller missing from " +
            "the list has nothing asserting its order at all");
    }
}
