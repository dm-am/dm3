import { formatDateFull } from "@/shared/lib/utils/datetime";
import { ONLINE_THRESHOLD_MS } from "@/shared/lib/constants/user";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import type { User, UserRef } from "./types";
import { UserRole } from "./types";

/**
 * Unified composable for user display logic
 * Single source of truth for tooltips and formatted info
 */
export function useUserDisplay() {
  // Cached timestamp for isOnline(); shared single timer, see useNowTimestamp
  const nowTimestamp = useNowTimestamp();

  /**
   * Check if user is online (last activity within the shared online threshold).
   * Optimized: uses cached timestamp instead of creating Date per call.
   */
  function isOnline(user: User | UserRef): boolean {
    if (!user.lastActivityUtc) return false;
    const lastActivityMs = new Date(user.lastActivityUtc).getTime();
    const diffMs = nowTimestamp.value - lastActivityMs;
    return diffMs < ONLINE_THRESHOLD_MS;
  }

  /**
   * Build online status tooltip with last activity time
   * @param user - User or UserRef object
   * @param onlineStatus - Pre-computed online status (use isOnline() result to ensure consistency with indicator)
   */
  function buildOnlineTooltip(
    user: User | UserRef,
    onlineStatus?: boolean,
  ): string {
    // System user - always available
    if (isSystemUser(user)) return "Робот-администратор";
    if (!user.lastActivityUtc) return "Активность неизвестна";
    // Use pre-computed status if provided, otherwise compute (for backwards compatibility)
    const online = onlineStatus ?? isOnline(user);
    if (online) return "Онлайн";
    return `Последняя активность: ${formatDateFull(user.lastActivityUtc)}`;
  }

  /**
   * Build registration date tooltip.
   * Note: registrationUtc is sent by no schema at all — it is recorded as such
   * in the contract test's UNSERVED list — so the fallback below never fires.
   * It is kept rather than deleted for the reason that list states: which side
   * gives way is a product decision, not one this file can make.
   */
  function buildRegistrationTooltip(user: User): string {
    const date = user.registeredUtc ?? user.registrationUtc;
    if (!date) return "Дата регистрации неизвестна";
    return `Регистрация: ${formatDateFull(date)}`;
  }

  /**
   * Check if user is a system user (Robot Administrator)
   */
  function isSystemUser(user: User | UserRef): boolean {
    return "role" in user && user.role === UserRole.System;
  }

  return {
    isOnline,
    buildOnlineTooltip,
    buildRegistrationTooltip,
  };
}
