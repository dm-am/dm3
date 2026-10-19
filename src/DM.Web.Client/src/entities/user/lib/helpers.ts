import { UserRole } from "../model/types";

/** One hierarchical role per user; there is no roles array on the wire. */
interface HasRole {
  role: UserRole;
}

/**
 * Whether two spellings of a username name one account: the address of a
 * profile may spell the name in another case than the session does.
 */
export function isSameUsername(
  a: string | null | undefined,
  b: string | null | undefined,
): boolean {
  return !!a && !!b && a.toLowerCase() === b.toLowerCase();
}

export function userIsAdmin(user: HasRole | null | undefined): boolean {
  return user?.role === UserRole.Admin;
}

export function userIsSeniorModerator(
  user: HasRole | null | undefined,
): boolean {
  if (!user) return false;
  return [UserRole.Admin, UserRole.SeniorModerator].includes(user.role);
}

export function userIsMentor(user: HasRole | null | undefined): boolean {
  if (!user) return false;
  return [
    UserRole.Admin,
    UserRole.SeniorModerator,
    UserRole.Moderator,
    UserRole.Mentor,
  ].includes(user.role);
}

export function userIsModerator(user: HasRole | null | undefined): boolean {
  if (!user) return false;
  return [
    UserRole.Admin,
    UserRole.SeniorModerator,
    UserRole.Moderator,
  ].includes(user.role);
}
