import { UserRole } from "@/shared/api/models/common";

/** In the order of the server's UserRole. */
const RANK: Record<UserRole, number> = {
  [UserRole.Guest]: 0,
  [UserRole.RegularUser]: 1,
  [UserRole.Mentor]: 2,
  [UserRole.Moderator]: 3,
  [UserRole.SeniorModerator]: 4,
  [UserRole.Admin]: 5,
  [UserRole.System]: 6,
};

type HasRole = { role: UserRole };

// ModerationRank.Reaches, so a screen does not offer an action the server refuses.
export function reachesByRank(
  actor: HasRole | null | undefined,
  target: HasRole | null | undefined,
): boolean {
  if (!actor || !target) return false;
  const actorRank = RANK[actor.role];
  const targetRank = RANK[target.role] ?? RANK[UserRole.Admin];
  return (
    actorRank >= RANK[UserRole.Moderator] &&
    targetRank < actorRank &&
    targetRank < RANK[UserRole.Admin]
  );
}
