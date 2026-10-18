using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Messaging.Features.Search;

/// <inheritdoc />
internal class MessageSearchService : IMessageSearchService
{
    private readonly IMessageSearchRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _userBlacklistChecker;

    // Inline operator tokens: from:/before:/after:/during:/in: followed by a
    // non-space value. Everything not matched stays as free-text tsquery input.
    private static readonly Regex OperatorRegex = new(
        @"\b(from|before|after|during|in):(\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MessageSearchService(
        IMessageSearchRepository repository,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker userBlacklistChecker)
    {
        _repository = repository;
        _identityProvider = identityProvider;
        _userBlacklistChecker = userBlacklistChecker;
    }

    /// <inheritdoc />
    public async Task<CursorResult<MessageSearchHit>> SearchAsync(
        MessageSearchRequest request, CancellationToken ct = default)
    {
        var query = Parse(request);
        var user = _identityProvider.Current.User;

        // "Скрывать сообщения в чате" hides the general chat messages of the
        // people on the reader's list wherever the reader reads that chat, and a
        // search over it is one more way of reading it: the chat left them out
        // and the search handed them back, a click away from the chat itself.
        // Only the general chat, as in MessageService.GetWithCursorAsync: a
        // private conversation is closed by BlockDirectMessages instead, and a
        // game room is the game's, not the reader's. A search scoped away from
        // the general chat has nobody to hide and asks nothing.
        var readsGeneralChat = query.Scopes.Count == 0 ||
                               query.Scopes.Any(s => s.Type == SearchSourceType.Global);
        if (user.IsAuthenticated && readsGeneralChat)
        {
            var hidden = await _userBlacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                user.UserId, UserBlacklistSettings.HideMessages, ct);
            query.HiddenAuthorIds = hidden.Count > 0 ? hidden : null;
        }

        return await _repository.Search(user.UserId, query, ct);
    }

    private static MessageSearchQuery Parse(MessageSearchRequest request)
    {
        var from = request.From;
        var after = request.After;
        var before = request.Before;
        var scopeTokens = new List<string>(request.In);

        // Extract inline operators, accumulating the free-text remainder.
        var freeText = OperatorRegex.Replace(request.Query ?? "", match =>
        {
            var op = match.Groups[1].Value.ToLowerInvariant();
            var value = match.Groups[2].Value;
            switch (op)
            {
                case "from":
                    from ??= value;
                    break;
                case "after":
                    if (TryParseDate(value, out var a)) after = a;
                    break;
                case "before":
                    if (TryParseDate(value, out var b)) before = b;
                    break;
                case "during":
                    if (TryParseDate(value, out var d))
                    {
                        after = d.Date;
                        before = d.Date.AddDays(1).AddTicks(-1);
                    }
                    break;
                case "in":
                    scopeTokens.Add(value);
                    break;
            }
            return " ";
        }).Trim();

        return new MessageSearchQuery
        {
            Text = freeText,
            FromUsername = string.IsNullOrWhiteSpace(from) ? null : from.Trim(),
            After = after,
            Before = before,
            Scopes = ParseScopes(scopeTokens),
            Cursor = request.Cursor,
            Limit = request.Limit
        };
    }

    private static List<SearchScope> ParseScopes(IEnumerable<string> tokens)
    {
        var scopes = new List<SearchScope>();
        foreach (var raw in tokens)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var token = raw.Trim();

            if (token.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Explicit "all" clears any restriction.
                return new List<SearchScope>();
            }

            if (token.Equals("global", StringComparison.OrdinalIgnoreCase))
            {
                scopes.Add(new SearchScope { Type = SearchSourceType.Global });
                continue;
            }

            var separator = token.IndexOf(':');
            if (separator <= 0 || separator == token.Length - 1) continue;

            var kind = token[..separator].ToLowerInvariant();
            var value = token[(separator + 1)..];
            var type = kind switch
            {
                "dm" or "chat" => (SearchSourceType?)SearchSourceType.Chat,
                "game" => SearchSourceType.Game,
                _ => null
            };
            if (type is null) continue;

            var scope = new SearchScope { Type = type.Value };
            if (Guid.TryParse(value, out var id)) scope.Id = id;
            else scope.RawId = value;
            scopes.Add(scope);
        }

        return scopes;
    }

    private static bool TryParseDate(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(
            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
}
