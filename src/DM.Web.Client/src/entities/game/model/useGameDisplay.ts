import { ref, onUnmounted } from "vue";
import type { Game, GameRef, Room } from "./types";
import { GameRole, RoomAccessPolicy, RoomAccessType } from "./types";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores/auth";

// Cached timestamp for isNew() optimization
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
 * Auth state for counter tooltip wording. Resolved lazily (only when a
 * counter tooltip is built) so the composable keeps working in contexts
 * without an active Pinia instance; guest wording is the safe default.
 * Reading the store inside render/computed keeps the value reactive.
 */
function isViewerAuthenticated(): boolean {
  try {
    return useAuthStore().isAuthenticated;
  } catch {
    return false;
  }
}

/**
 * Unified composable for game display logic
 * Single source of truth for tooltips, counters, and formatted info
 */
export function useGameDisplay() {
  // Start timestamp refresh when composable is used
  startTimestampRefresh();

  // Stop when component unmounts
  onUnmounted(() => {
    stopTimestampRefresh();
  });
  /**
   * Format player count (current/limit)
   * Shows "∞" when no limit is set (unlimited)
   */
  function formatPcCount(game: Game | GameRef): string {
    const current = game.recruitment?.pcCount ?? 0;
    const limit = game.recruitment?.pcLimit;
    return limit != null ? `${current}/${limit}` : `${current}/∞`;
  }

  /**
   * Get status date label based on game status and closedReason
   */
  function getStatusDateLabel(game: Game): string {
    if (game.status === "Draft") return "Дата создания";
    if (game.status === "Active") return "Дата начала";
    if (game.status === "Closed") {
      if (game.closedReason === "Finished") return "Дата завершения";
      if (game.closedReason === "Frozen") return "Дата заморозки";
      return "Дата закрытия";
    }
    return "Дата";
  }

  /**
   * Get the relevant date for game status
   */
  function getStatusDate(game: Game): string | undefined {
    if (game.status === "Draft") return game.createdUtc;
    if (game.status === "Active") return game.activatedUtc;
    if (game.status === "Closed") return game.closedUtc;
    return undefined;
  }

  /**
   * Format status date (dd.MM.yyyy)
   */
  function formatStatusDate(game: Game): string {
    return formatDate(getStatusDate(game));
  }

  /**
   * Format status date with time for tooltip (DD.MM.YYYY в HH:mm)
   * Uses the shared sitewide date-time format.
   */
  function formatStatusDateFull(game: Game): string {
    return formatDateFull(getStatusDate(game));
  }

  /**
   * Build status tooltip with multiple dates based on status:
   * - Draft: creation date
   * - Active: creation date + start date (+ recruitment date if recruiting)
   * - Closed: creation date + start date + close/freeze/finish date
   */
  function buildStatusTooltip(game: Game): string {
    const lines: string[] = [];

    // Creation date - always shown
    lines.push(`Создание игры: ${formatDateFull(game.createdUtc)}`);

    // Start date - for Active and Closed
    if (game.status === "Active" || game.status === "Closed") {
      lines.push(`Начало игры: ${formatDateFull(game.activatedUtc)}`);
    }

    // Recruitment date - for Active games with open recruitment
    if (game.status === "Active" && game.recruitment?.isOpen) {
      lines.push(
        `Начало последнего набора: ${formatDateFull(game.recruitment.startedUtc)}`,
      );
    }

    // Close date - only for Closed
    if (game.status === "Closed") {
      lines.push(`Закрытие игры: ${formatDateFull(game.closedUtc)}`);
    }

    return lines.join("\n");
  }

  /**
   * Build game tooltip with labels (multiline):
   * Мастер: Username
   * Ассистент(ы): A, B
   * Персонажи: X/Y
   * Читатели: Z
   */
  function buildTooltip(game: Game | GameRef): string {
    const parts: string[] = [];

    // Мастер: Username
    if (game.master?.username) {
      parts.push(`Мастер: ${game.master.username}`);
    }

    // Ассистент(ы): Username, ... (if any)
    const assistants = game.assistants?.filter((a) => a?.username) ?? [];
    if (assistants.length > 0) {
      const label = assistants.length === 1 ? "Ассистент" : "Ассистенты";
      parts.push(`${label}: ${assistants.map((a) => a.username).join(", ")}`);
    }

    // Персонажи: X/Y
    parts.push(`Персонажи: ${formatPcCount(game)}`);

    // Читатели: Z
    const totalReaders = game.subscribersCount ?? 0;
    parts.push(`Читатели: ${totalReaders}`);

    return parts.join("\n");
  }

  /**
   * Collect participant lines for a room tooltip.
   *
   * The tooltip lists ONLY player characters (PC). NPCs are GM-run
   * entities that don't participate in the "who can write here" sense
   * the user is after, so they're filtered out of every code path:
   *
   * - Open rooms: sourced from game.activeCharacters (backend already
   *   filters `!c.IsNpc` in GameRepository.EnrichGamesAsync, so the
   *   list is PC-only by construction).
   * - Private rooms: sourced from room.accesses — each grant is
   *   either a character (character + owner pair) or a reader (user
   *   only, rendered as "- username — читатель"). Characters whose
   *   `isNpc` flag is true are skipped here. If `room.accesses` is
   *   missing, falls back to `room.claims` for character-based
   *   entries, same NPC filter applied.
   */
  function collectRoomParticipants(
    room: Room,
    game?: Game | GameRef,
  ): string[] {
    const lines: string[] = [];

    if (room.access === RoomAccessType.Private) {
      const accesses = room.accesses ?? [];
      if (accesses.length > 0) {
        for (const a of accesses) {
          if (a.character?.name) {
            // PC-only: skip NPCs (GM-run characters should not appear
            // in the "who can post here" tooltip).
            if (a.character.isNpc) continue;
            // Characters: Full = can read + post, ReadOnly = read only.
            // Full is the common case, so we mark only ReadOnly explicitly.
            const owner = a.character.author?.username ?? "?";
            const suffix =
              a.policy === RoomAccessPolicy.ReadOnly ? " — чтение" : "";
            lines.push(`- ${a.character.name} (${owner})${suffix}`);
          } else if (a.user?.username) {
            // Readers are always ReadOnly by definition.
            lines.push(`- ${a.user.username} — читатель`);
          }
        }
        return lines;
      }
      // Fallback: claims-only (no accesses returned).
      // Claims do not carry policy info, so everything is rendered plainly.
      for (const c of room.claims ?? []) {
        if (c.character?.isNpc) continue; // PC-only
        const name = c.character?.name ?? "?";
        const owner = c.character?.author?.username ?? "?";
        lines.push(`- ${name} (${owner})`);
      }
      return lines;
    }

    // Open rooms: list all active characters in the game. Backend
    // filters NPCs in the ActiveCharacters batch query, so this list
    // is already PC-only.
    for (const c of game?.activeCharacters ?? []) {
      lines.push(`- ${c.name} (${c.ownerUsername})`);
    }
    return lines;
  }

  /**
   * Whether the current viewer (identified by username) has access to a
   * room. Open rooms are accessible to everyone. Private rooms are
   * accessible to:
   *   - game master / assistants / mentor (via game.participation)
   *   - owners of characters listed in room.claims
   *   - characters' owners and readers listed in room.accesses
   */
  function canViewRoom(
    room: Room,
    game?: Game | GameRef,
    viewerUsername?: string,
  ): boolean {
    if (room.access !== RoomAccessType.Private) return true;

    const participation = game?.participation;
    if (
      participation?.includes(GameRole.Master) ||
      participation?.includes(GameRole.Assistant) ||
      participation?.includes(GameRole.Mentor)
    ) {
      return true;
    }

    if (!viewerUsername) return false;

    // Explicit accesses: reader user OR character's owner
    if (
      room.accesses?.some(
        (a) =>
          a.user?.username === viewerUsername ||
          a.character?.author?.username === viewerUsername,
      )
    ) {
      return true;
    }

    // Claims fallback (character owners only)
    return (
      room.claims?.some(
        (c) => c.character?.author?.username === viewerUsername,
      ) ?? false
    );
  }

  /**
   * Build room tooltip with participants list and access type.
   * Unified format used across the whole app (sidebar, featured posts,
   * Pulse, game page). Matches the characters tooltip on the games table:
   *
   *   Персонажи:
   *   - CharName (ownerUsername)
   *   - CharName (ownerUsername)
   *
   *   Доступ: открытый
   *
   * Returns an empty string when the viewer should not see a tooltip
   * (i.e. private room without access) — callers should skip rendering
   * the Tooltip wrapper when this returns empty.
   */
  function buildRoomTooltip(
    room: Room,
    game?: Game | GameRef,
    viewerUsername?: string,
  ): string {
    if (!canViewRoom(room, game, viewerUsername)) return "";

    const parts: string[] = [];

    const participantLines = collectRoomParticipants(room, game);
    if (participantLines.length > 0) {
      parts.push("Персонажи:");
      parts.push(...participantLines);
      parts.push(""); // blank line separator before "Доступ"
    }

    const isPrivate = room.access === RoomAccessType.Private;
    parts.push(`Доступ: ${isPrivate ? "закрытый" : "открытый"}`);

    return parts.join("\n");
  }

  /**
   * Get unread posts count with null safety
   */
  function getUnreadPosts(game: Game | GameRef): number {
    return game.unreadPostsCount ?? 0;
  }

  /**
   * Get unread comments count with null safety
   */
  function getUnreadComments(game: Game | GameRef): number {
    return game.unreadCommentsCount ?? 0;
  }

  /**
   * Format posts counter tooltip.
   * For guests the backend puts TOTAL counts into unreadPostsCount
   * (nothing can be marked as read), so the wording must not claim
   * "непрочитанных". Authorized users keep the unread wording.
   */
  function formatUnreadPostsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных постов: ${count}`
      : `Постов: ${count}`;
  }

  /**
   * Format comments counter tooltip (same guest/authorized split as posts).
   */
  function formatUnreadCommentsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных комментариев: ${count}`
      : `Комментариев: ${count}`;
  }

  /**
   * Check if game has any unread content
   */
  function hasUnread(game: Game | GameRef): boolean {
    return getUnreadPosts(game) > 0 || getUnreadComments(game) > 0;
  }

  /**
   * Format status for display (matches filter labels)
   */
  function formatStatus(game: Game | GameRef): string {
    if (game.status === "Draft") {
      return "Оформляется";
    }

    if (game.status === "Active") {
      return game.recruitment?.isOpen ? "Набор игроков" : "Идет игра";
    }

    if (game.status === "Closed") {
      if (game.closedReason === "Finished") return "Завершена";
      if (game.closedReason === "Frozen") return "Заморожена";
      return "Закрыта";
    }

    return String(game.status);
  }

  /**
   * Get status CSS class for styling
   */
  function getStatusClass(game: Game | GameRef): string {
    if (game.status === "Draft") return "draft";
    if (game.status === "Active") return "active";
    if (game.status === "Closed") return "closed";
    return "";
  }

  /**
   * Check if game was activated (became Active) less than 7 days ago
   * Used to highlight "new" games with green color
   * Optimized: uses cached timestamp instead of creating Date per call
   */
  function isNew(game: Game | GameRef): boolean {
    if (!game.activatedUtc) return false;
    const activatedMs = new Date(game.activatedUtc).getTime();
    const diffMs = nowTimestamp.value - activatedMs;
    const sevenDaysMs = 7 * 24 * 60 * 60 * 1000;
    return diffMs < sevenDaysMs;
  }

  /**
   * Format slots display for status column: "[N/M]" or "[N/∞]"
   * Moved verbatim from widgets/games-table/GamesDataTable.vue (SSOT extraction).
   */
  function formatSlots(row: {
    recruitment?: { pcCount: number; pcLimit?: number | null };
  }): string {
    const pcCount = row.recruitment?.pcCount ?? 0;
    const pcLimit = row.recruitment?.pcLimit;
    return pcLimit != null ? `[${pcCount}/${pcLimit}]` : `[${pcCount}/∞]`;
  }

  /**
   * Build slots tooltip with active characters list and free-slots summary.
   * Moved verbatim from widgets/games-table/GamesDataTable.vue (SSOT extraction).
   */
  function buildSlotsTooltip(row: {
    recruitment?: { pcCount: number; pcLimit?: number | null };
    activeCharacters?: { name: string; ownerUsername: string }[];
  }): string {
    const chars = row.activeCharacters ?? [];
    const pcCount = row.recruitment?.pcCount ?? 0;
    const pcLimit = row.recruitment?.pcLimit;

    const lines: string[] = [];

    // Characters
    if (chars.length > 0) {
      lines.push("Персонажи:");
      chars.forEach((c) => lines.push(`- ${c.name} (${c.ownerUsername})`));
    } else {
      lines.push("Нет персонажей");
    }

    // Free slots
    if (pcLimit != null) {
      const free = Math.max(0, pcLimit - pcCount);
      if (free > 0) {
        lines.push(`\nСвободных мест: ${free}`);
      } else {
        lines.push("\nМест нет");
      }
    } else {
      lines.push("\nМест: без ограничений");
    }

    return lines.join("\n");
  }

  /**
   * Build assistant(s) tooltip: "Ассистент: X" / "Ассистенты: X, Y".
   * Moved verbatim from widgets/games-table/GamesDataTable.vue (SSOT extraction).
   */
  function buildAssistantTooltip(assistants: { username: string }[]): string {
    const names = assistants.map((a) => a.username).join(", ");
    return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
  }

  return {
    formatPcCount,
    buildTooltip,
    buildStatusTooltip,
    buildRoomTooltip,
    canViewRoom,
    getUnreadPosts,
    getUnreadComments,
    formatUnreadPostsTooltip,
    formatUnreadCommentsTooltip,
    hasUnread,
    formatStatus,
    getStatusClass,
    getStatusDateLabel,
    formatStatusDate,
    formatStatusDateFull,
    isNew,
    formatSlots,
    buildSlotsTooltip,
    buildAssistantTooltip,
  };
}
