import { UserRole } from "@/shared/api/models/common";

/** The site roles in the order of the server's enumeration (UserRole.cs). */
const RANK: Record<UserRole, number> = {
  [UserRole.Guest]: 0,
  [UserRole.RegularUser]: 1,
  [UserRole.Mentor]: 2,
  [UserRole.Moderator]: 3,
  [UserRole.SeniorModerator]: 4,
  [UserRole.Admin]: 5,
  [UserRole.System]: 6,
};

/** Anything that carries a site role: the reader, the author of a text. */
type HasRole = { role: UserRole };

/**
 * Whether a moderation action of the actor reaches the target: a warning, a
 * ban. Strictly below the actor's own rank, and never an administrator, the
 * server's ModerationRank.Reaches. The actor has to be a moderator at least:
 * below that no rank warns anybody.
 *
 * The server is the one that refuses; this answers whether a screen offers the
 * action at all, so that a moderator is not shown "Предупреждение" under the
 * text of a senior moderator or an administrator only to be refused (the
 * owner, 05.10: what a user may not do is not shown). The rule reads two roles
 * the page already holds, the reader's and the author's, which is why it is
 * not one more flag on every comment.
 */
export function reachesByRank(
  actor: HasRole | null | undefined,
  target: HasRole | null | undefined,
): boolean {
  if (!actor || !target) return false;
  const actorRank = RANK[actor.role] ?? 0;
  const targetRank = RANK[target.role] ?? RANK[UserRole.Admin];
  return (
    actorRank >= RANK[UserRole.Moderator] &&
    targetRank < actorRank &&
    targetRank < RANK[UserRole.Admin]
  );
}
