using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.Messages;

internal static class HiddenAuthors
{
    /// <summary>
    /// The authors whose general chat messages the reader hides ("Скрывать сообщения в
    /// чате"), null when there are none: the general chat and the search over it leave
    /// them out alike.
    /// </summary>
    public static async Task<IReadOnlyCollection<Guid>?> HiddenAuthorsAsync(
        this IUserBlacklistChecker checker, Guid readerId, CancellationToken ct)
    {
        var hidden = await checker.GetBlockedUserIdsIfFlagEnabledAsync(
            readerId, UserBlacklistSettings.HideMessages, ct);
        return hidden.Count > 0 ? hidden : null;
    }
}
