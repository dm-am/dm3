import { UserRole } from "@/api/models/community";
import type { User } from "@/api/models/community";

export function userIsAdmin(user: User | null): boolean {
  return user !== null && user.roles?.some((r) => r === UserRole.Admin) === true;
}

export function userIsHighAuthority(user: User | null): boolean {
  return (
    user !== null &&
    user.roles?.some(
      (r) => r === UserRole.Admin || r === UserRole.SeniorModerator,
    ) === true
  );
}

export function userIsAuthority(user: User | null): boolean {
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
