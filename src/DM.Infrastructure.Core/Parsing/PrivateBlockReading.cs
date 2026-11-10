using System;
using System.Collections.Generic;
using System.Linq;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Infrastructure.Core.Parsing.Visitors;

namespace DM.Infrastructure.Core.Parsing;

/// <inheritdoc />
/// <remarks>
/// One tree is read twice: whole, and through the filter the page of this reader is
/// rendered through (<see cref="PermissionFilter"/>). What the filtered reading has
/// lost is what the blocks keep from him, told apart by the one reading of them the
/// page has.
///
/// The endpoint behind the address of a file reads its identifier in either
/// spelling, so a mention is either, wherever it stands: reading more than the
/// address errs towards keeping the file from a reader who does not read the block.
/// </remarks>
public sealed class PrivateBlockReading : IPrivateBlockReading
{
    private readonly IBbParserProvider _parserProvider;

    /// <inheritdoc cref="PrivateBlockReading"/>
    public PrivateBlockReading(IBbParserProvider parserProvider) => _parserProvider = parserProvider;

    /// <inheritdoc />
    public IReadOnlySet<Guid> HiddenFrom(
        IAuthorizationSubject? reader, PrivateBlockText text, IReadOnlyCollection<Guid> uploadIds)
    {
        // A text with no hiding markup has no block to keep anything in, and is not parsed.
        if (uploadIds.Count == 0 || !PrivateBlockMarkup.ContainsPrivateMarkup(text.Source))
        {
            return new HashSet<Guid>();
        }

        var (whole, read) = Read(text.Source, ContextOf(reader, text));
        return Lost(whole, read, uploadIds);
    }

    /// <inheritdoc />
    /// <remarks>Read as a plain-text channel reads it: every hiding tag cut out, whoever would read it.</remarks>
    public IReadOnlySet<Guid> OnlyInsideBlocks(string? source, IReadOnlyCollection<Guid> uploadIds)
    {
        if (uploadIds.Count == 0 || !PrivateBlockMarkup.ContainsPrivateMarkup(source))
        {
            return new HashSet<Guid>();
        }

        var (whole, outside) = Read(source, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost });
        return Lost(whole, outside, uploadIds);
    }

    /// <inheritdoc />
    public bool ReadsEveryBlock(IAuthorizationSubject? reader, PrivateBlockText text)
    {
        if (!PrivateBlockMarkup.ContainsPrivateMarkup(text.Source))
        {
            return true;
        }

        var (whole, read) = Read(text.Source, ContextOf(reader, text));
        return string.Equals(whole, read, StringComparison.Ordinal);
    }

    private (string Whole, string Read) Read(string? text, RenderContext context)
    {
        var source = text ?? string.Empty;
        try
        {
            var tree = _parserProvider.GetForSurface(BbSurface.GamePost).Parse(source);
            return (tree.ToBb(), tree.ToBb(PermissionFilter.Prepare(context).Filter));
        }
        catch (BbParserException)
        {
            // The page renders a text the parser refuses as nothing, so nobody reads a mention in it.
            return (source, string.Empty);
        }
    }

    private static RenderContext ContextOf(IAuthorizationSubject? reader, PrivateBlockText text) => new()
    {
        Viewer = reader,
        Audience = RenderAudience.Display,
        Surface = BbSurface.GamePost,
        PostAuthorUserId = text.AuthorUserId,
        PrivateAddresseeOwnerUserIdsByAttribute =
            PrivateAddresseeSnapshot.Read(text.PrivateAddresseeSnapshotJson).OwnerUserIdsByAttribute,
        GameLeadUserIds = text.GameLeadUserIds,
        GameShowPrivateMessages = text.GameShowPrivateMessages,
        RoomViewPrivateText = text.RoomViewPrivateText
    };

    private static HashSet<Guid> Lost(string whole, string read, IReadOnlyCollection<Guid> uploadIds) =>
        uploadIds.Where(id => Mentions(whole, id) && !Mentions(read, id)).ToHashSet();

    /// <summary>Whether the text names the upload, in either spelling of its identifier.</summary>
    internal static bool Mentions(string text, Guid uploadId) =>
        text.Contains(uploadId.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
        text.Contains(uploadId.ToString("N"), StringComparison.OrdinalIgnoreCase);
}
