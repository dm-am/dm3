import { UserRole } from "../model/types";
import type { User } from "../model/types";

export function userIsAdmin(user: User | null): boolean {
  return (
    user !== null && user.roles?.some((r) => r === UserRole.Admin) === true
  );
}

export function userIsSeniorModerator(user: User | null): boolean {
  return (
    user !== null &&
    user.roles?.some(
      (r) => r === UserRole.Admin || r === UserRole.SeniorModerator,
    ) === true
  );
}

export function userIsModerator(user: User | null): boolean {
  return (
    user !== null &&
    user.roles?.some(
      (r) =>
        r === UserRole.Admin ||
        r === UserRole.SeniorModerator ||
        r === UserRole.Moderator,
    ) === true
  );
}
