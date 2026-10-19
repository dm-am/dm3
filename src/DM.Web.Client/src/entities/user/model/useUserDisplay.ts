import { formatDateFull } from "@/shared/lib/utils/datetime";
import { ONLINE_THRESHOLD_MS } from "@/shared/lib/constants/user";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import type { User, UserRef } from "./types";
import { UserRole } from "./types";

export function useUserDisplay() {
  const nowTimestamp = useNowTimestamp();

  function isOnline(user: User | UserRef): boolean {
    if (!user.lastActivityUtc) return false;
    const lastActivityMs = new Date(user.lastActivityUtc).getTime();
    const diffMs = nowTimestamp.value - lastActivityMs;
    return diffMs < ONLINE_THRESHOLD_MS;
  }

  /** @param onlineStatus The indicator's own answer, so the two agree. */
  function buildOnlineTooltip(
    user: User | UserRef,
    onlineStatus?: boolean,
  ): string {
    if (isSystemUser(user)) return "Робот-администратор";
    if (!user.lastActivityUtc) return "Активность неизвестна";
    const online = onlineStatus ?? isOnline(user);
    if (online) return "Онлайн";
    return `Последняя активность: ${formatDateFull(user.lastActivityUtc)}`;
  }

  function buildRegistrationTooltip(user: User): string {
    const date = user.registeredUtc;
    if (!date) return "Дата регистрации неизвестна";
    return `Регистрация: ${formatDateFull(date)}`;
  }

  function isSystemUser(user: User | UserRef): boolean {
    return "role" in user && user.role === UserRole.System;
  }

  return {
    isOnline,
    buildOnlineTooltip,
    buildRegistrationTooltip,
  };
}
