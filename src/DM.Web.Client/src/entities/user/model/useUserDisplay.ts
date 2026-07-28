import { ref, onUnmounted } from "vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { ONLINE_THRESHOLD_MS } from "@/shared/lib/constants/user";
import type { User, UserRef } from "./types";
import { UserRole } from "./types";

// Cached timestamp for isOnline() optimization
// Refreshed every minute to avoid creating Date objects per-row
const nowTimestamp = ref(Date.now());
let intervalId: ReturnType<typeof setInterval> | null = null;
let instanceCount = 0;

function startTimestampRefresh() {
  if (intervalId === null) {
    intervalId = setInterval(() => {
      nowTimestamp.value = Date.now();
    }, 60_000); // Refresh every minute
  }
  instanceCount++;
}

function stopTimestampRefresh() {
  instanceCount--;
  if (instanceCount <= 0 && intervalId !== null) {
    clearInterval(intervalId);
    intervalId = null;
    instanceCount = 0;
  }
}

/**
 * Unified composable for user display logic
 * Single source of truth for tooltips and formatted info
 */
export function useUserDisplay() {
  // Start timestamp refresh when composable is used
  startTimestampRefresh();

  // Stop when component unmounts
  onUnmounted(() => {
    stopTimestampRefresh();
  });

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
   * Build registration date tooltip
   * Note: API may return registeredUtc (community list) or registrationUtc (profile)
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
