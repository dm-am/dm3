using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// The single place where the state of a module (a game, a blog) is turned into
/// the answer "may a user holding no role in it see the module at all":
/// <list type="bullet">
/// <item>premoderation hides the module outright, whatever its status: nothing
/// awaiting a curator or returned for edits is public;</item>
/// <item>a draft is hidden unless its author opened the preview
/// (<see cref="DraftVisibility.Public" />), which is the only thing that setting
/// is for.</item>
/// </list>
/// Roles are answered on top of this rule and are not part of it: leads,
/// curators, invited users and site moderation are each a separate question.
/// </summary>
/// <remarks>
/// Storage filters have to restate the same condition as an expression tree,
/// because EF Core translates trees and not method calls. Those copies are kept
/// equal to this one by GameAccessibilityFiltersShould, which compares them over
/// every combination of the three fields the rule reads.
/// </remarks>
public static class ModuleVisibility
{
    /// <summary>
    /// Whether the module is visible to a user holding no role in it
    /// </summary>
    /// <param name="status">Module status</param>
    /// <param name="premoderationStatus">Premoderation status</param>
    /// <param name="draftVisibility">Visibility of the draft preview</param>
    /// <returns>Whether the module may be listed and opened by anyone</returns>
    public static bool IsPubliclyVisible(
        ModuleStatus status,
        PremoderationStatus premoderationStatus,
        DraftVisibility draftVisibility) =>
        premoderationStatus == PremoderationStatus.Approved &&
        (status != ModuleStatus.Draft || draftVisibility == DraftVisibility.Public);
}
