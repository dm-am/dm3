import { UserRole } from "../model/types";

/**
 * Minimal shape carrying the site role. The backend exposes a SINGLE
 * hierarchical role on every user DTO (UserRef.role / User.role) — there is
 * no roles array on the wire. Structural typing keeps these helpers usable
 * for UserRef, User and profile shapes alike.
 */
interface HasRole {
  role: UserRole;
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

export function userIsModerator(user: HasRole | null | undefined): boolean {
  if (!user) return false;
  return [
    UserRole.Admin,
    UserRole.SeniorModerator,
    UserRole.Moderator,
  ].includes(user.role);
}
