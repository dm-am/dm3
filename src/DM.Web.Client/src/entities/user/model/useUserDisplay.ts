import { ref, onUnmounted } from "vue";
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
 * Role badge configuration
 * Short form: [А], [С], [М], [Н] - gray brackets, green bold letter
 */
type RoleBadge = {
  /** Short badge letter (А, С, М, Н) */
  letter: string;
  /** Full label for tooltips */
  label: string;
  /** CSS class for styling */
  cssClass: string;
};

const ROLE_BADGES: Partial<Record<UserRole, RoleBadge>> = {
  [UserRole.Admin]: { letter: "А", label: "Администратор", cssClass: "role-admin" },
  [UserRole.SeniorModerator]: { letter: "С", label: "Старший модератор", cssClass: "role-senior-moderator" },
  [UserRole.Moderator]: { letter: "М", label: "Модератор", cssClass: "role-moderator" },
  [UserRole.Mentor]: { letter: "Н", label: "Наставник", cssClass: "role-mentor" },
  [UserRole.System]: { letter: "Р", label: "Робот-администратор", cssClass: "role-system" },
};

const ROLE_FULL_NAMES: Partial<Record<UserRole, string>> = {
  [UserRole.Admin]: "Администратор",
  [UserRole.SeniorModerator]: "Старший модератор",
  [UserRole.Moderator]: "Модератор",
  [UserRole.Mentor]: "Наставник",
  [UserRole.RegularUser]: "Пользователь",
  [UserRole.Guest]: "Гость",
  [UserRole.System]: "Робот-администратор",
};

/**
 * Unified composable for user display logic
 * Single source of truth for tooltips, badges, and formatted info
 */
export function useUserDisplay() {
  // Start timestamp refresh when composable is used
  startTimestampRefresh();

  // Stop when component unmounts
  onUnmounted(() => {
    stopTimestampRefresh();
  });

  /**
   * Online threshold: 10 minutes
   */
  const ONLINE_THRESHOLD_MS = 10 * 60 * 1000;

  /**
   * Check if user is online (last activity < 10 min ago)
   * Optimized: uses cached timestamp instead of creating Date per call
   */
  function isOnline(user: User | UserRef): boolean {
    if (!user.lastActivityUtc) return false;
    const lastActivityMs = new Date(user.lastActivityUtc).getTime();
    const diffMs = nowTimestamp.value - lastActivityMs;
    return diffMs < ONLINE_THRESHOLD_MS;
  }

  /**
   * Get role badge info (only for staff roles)
   */
  function getRoleBadge(user: User): RoleBadge | null {
    return ROLE_BADGES[user.role] ?? null;
  }

  /**
   * Honorary badge: [П] with bold gray letter
   */
  const HONORARY_BADGE = { letter: "П", label: "Почетный пользователь", cssClass: "status-honorary" };

  /**
   * Get honorary badge if user has honorary status
   */
  function getHonoraryBadge(user: User): typeof HONORARY_BADGE | null {
    return user.isHonorary ? HONORARY_BADGE : null;
  }

  /**
   * Get full role name for tooltips
   */
  function getRoleFullName(user: User): string {
    return ROLE_FULL_NAMES[user.role] ?? String(user.role);
  }

  /**
   * Format rating for display
   * Shows quality/quantity: "123 / 45" or "—" if disabled
   */
  function formatRating(user: User): string {
    if (!user.rating) return "—";
    const quality = user.rating.totalRating ?? user.rating.postReviewScoreSum ?? 0;
    const quantity = user.rating.totalPosts ?? 0;
    return `${quality} / ${quantity}`;
  }

  /**
   * Build rating tooltip: "Качество: X | Количество: Y"
   */
  function buildRatingTooltip(user: User): string {
    if (!user.rating) return "Рейтинг скрыт";
    const quality = user.rating.totalRating ?? user.rating.postReviewScoreSum ?? 0;
    const quantity = user.rating.totalPosts ?? 0;
    return `Качество: ${quality} | Количество: ${quantity}`;
  }

  /**
   * Format a date string to dd.MM.yyyy HH:mm
   */
  function formatDateFull(dateStr: string | null | undefined): string {
    if (!dateStr) return "—";
    const d = new Date(dateStr);
    const day = String(d.getDate()).padStart(2, "0");
    const month = String(d.getMonth() + 1).padStart(2, "0");
    const year = d.getFullYear();
    const hours = String(d.getHours()).padStart(2, "0");
    const minutes = String(d.getMinutes()).padStart(2, "0");
    return `${day}.${month}.${year} ${hours}:${minutes}`;
  }

  /**
   * Format a date string to dd.MM.yyyy
   */
  function formatDateShort(dateStr: string | null | undefined): string {
    if (!dateStr) return "—";
    const d = new Date(dateStr);
    const day = String(d.getDate()).padStart(2, "0");
    const month = String(d.getMonth() + 1).padStart(2, "0");
    const year = d.getFullYear();
    return `${day}.${month}.${year}`;
  }

  /**
   * Build user tooltip for lists (multiline):
   * Роль: Админ
   * Почетный
   * Новичок
   * Рейтинг: X/Y
   */
  function buildTooltip(user: User): string {
    const parts: string[] = [];

    // Role (always show)
    parts.push(`Роль: ${getRoleFullName(user)}`);

    // Honorary status
    if (user.isHonorary) {
      parts.push("Почетный");
    }

    // Newbie status
    if (user.isNewbie) {
      parts.push("Новичок");
    }

    // Rating (if enabled)
    if (user.rating) {
      const quality = user.rating.totalRating ?? user.rating.postReviewScoreSum ?? 0;
      const quantity = user.rating.totalPosts ?? 0;
      parts.push(`Рейтинг: ${quality}/${quantity}`);
    }

    return parts.join("\n");
  }

  /**
   * Build online status tooltip with last activity time
   * @param user - User or UserRef object
   * @param onlineStatus - Pre-computed online status (use isOnline() result to ensure consistency with indicator)
   */
  function buildOnlineTooltip(user: User | UserRef, onlineStatus?: boolean): string {
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
   * Get all status badges for user (role, honorary, newbie)
   */
  function getStatusBadges(user: User): Array<{ label: string; cssClass: string }> {
    const badges: Array<{ label: string; cssClass: string }> = [];

    // Role badge (if staff)
    const roleBadge = getRoleBadge(user);
    if (roleBadge) {
      badges.push(roleBadge);
    }

    // Honorary badge
    if (user.isHonorary) {
      badges.push({ label: "Почетный", cssClass: "status-honorary" });
    }

    // Newbie badge
    if (user.isNewbie) {
      badges.push({ label: "Новичок", cssClass: "status-newbie" });
    }

    return badges;
  }

  /**
   * Check if user has any staff role (Admin, Moderator, etc.)
   */
  function isStaff(user: User): boolean {
    return (
      user.role === UserRole.Admin ||
      user.role === UserRole.SeniorModerator ||
      user.role === UserRole.Moderator ||
      user.role === UserRole.Mentor
    );
  }

  /**
   * Check if user is moderator or higher
   */
  function isModerator(user: User): boolean {
    return (
      user.role === UserRole.Admin ||
      user.role === UserRole.SeniorModerator ||
      user.role === UserRole.Moderator
    );
  }

  /**
   * Check if user is a system user (Robot Administrator)
   */
  function isSystemUser(user: User | UserRef): boolean {
    return "role" in user && user.role === UserRole.System;
  }

  return {
    isOnline,
    isSystemUser,
    getRoleBadge,
    getHonoraryBadge,
    getRoleFullName,
    formatRating,
    buildRatingTooltip,
    formatDateFull,
    formatDateShort,
    buildTooltip,
    buildOnlineTooltip,
    buildRegistrationTooltip,
    getStatusBadges,
    isStaff,
    isModerator,
  };
}
