import { GameStatus, type Game, type GameRef } from "./gameTypes";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import { useAuthStore } from "@/shared/stores/auth";

// What reads the clock stays in the composable; the wording is in the functions below.
export function useGameDisplay() {
  const nowTimestamp = useNowTimestamp();

  /** Started less than seven days ago; a subscription row carries the date alone. */
  function isNew(game: Pick<Game | GameRef, "activatedUtc">): boolean {
    if (!game.activatedUtc) return false;
    const activatedMs = new Date(game.activatedUtc).getTime();
    const diffMs = nowTimestamp.value - activatedMs;
    const sevenDaysMs = 7 * 24 * 60 * 60 * 1000;
    return diffMs < sevenDaysMs;
  }

  /** The green "new" of a list: a closed game never gets it. */
  function isNewHighlight(
    game: Pick<Game, "activatedUtc" | "status">,
  ): boolean {
    return game.status !== GameStatus.Closed && isNew(game);
  }

  return {
    formatPcCount,
    buildTooltip,
    buildStatusTooltip,
    getUnreadPosts,
    getUnreadComments,
    formatUnreadPostsTooltip,
    formatUnreadCommentsTooltip,
    hasUnread,
    formatStatus,
    isNew,
    isNewHighlight,
    formatSlots,
    buildSlotsTooltip,
    buildAssistantTooltip,
  };
}

function buildStatusTooltip(game: Game): string {
  const lines: string[] = [];

  lines.push(`Создание игры: ${formatDateFull(game.createdUtc)}`);

  if (game.status === "Active" || game.status === "Closed") {
    lines.push(`Начало игры: ${formatDateFull(game.activatedUtc)}`);
  }

  if (game.status === "Active" && game.recruitment?.isOpen) {
    lines.push(
      `Начало последнего набора: ${formatDateFull(game.recruitment.startedUtc)}`,
    );
  }

  if (game.status === "Closed") {
    lines.push(`Закрытие игры: ${formatDateFull(game.closedUtc)}`);
  }

  return lines.join("\n");
}

/** The words of the filter. */
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

/** What a row of the slots column needs of a game. */
type Slots = { recruitment?: { pcCount: number; pcLimit?: number | null } };

function formatPcCount(game: Slots): string {
  const current = game.recruitment?.pcCount ?? 0;
  const limit = game.recruitment?.pcLimit;
  return limit != null ? `${current}/${limit}` : `${current}/∞`;
}

function buildTooltip(game: Game | GameRef): string {
  const parts: string[] = [];

  if (game.master?.username) {
    parts.push(`Мастер: ${game.master.username}`);
  }

  const assistants = game.assistants?.filter((a) => a?.username) ?? [];
  if (assistants.length > 0) {
    const label = assistants.length === 1 ? "Ассистент" : "Ассистенты";
    parts.push(`${label}: ${assistants.map((a) => a.username).join(", ")}`);
  }

  parts.push(`Персонажи: ${formatPcCount(game)}`);

  const totalReaders = game.subscribersCount ?? 0;
  parts.push(`Читатели: ${totalReaders}`);

  return parts.join("\n");
}

function formatSlots(row: Slots): string {
  return `[${formatPcCount(row)}]`;
}

function buildSlotsTooltip(
  row: Slots & {
    activeCharacters?: { name: string; ownerUsername: string }[];
  },
): string {
  const chars = row.activeCharacters ?? [];
  const pcCount = row.recruitment?.pcCount ?? 0;
  const pcLimit = row.recruitment?.pcLimit;

  const lines: string[] = [];

  if (chars.length > 0) {
    lines.push("Персонажи:");
    chars.forEach((c) => lines.push(`- ${c.name} (${c.ownerUsername})`));
  } else {
    lines.push("Нет персонажей");
  }

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

function buildAssistantTooltip(assistants: { username: string }[]): string {
  const names = assistants.map((a) => a.username).join(", ");
  return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
}

function isViewerAuthenticated(): boolean {
  // Without an installed pinia the guest wording is the safe answer.
  try {
    return useAuthStore().isAuthenticated;
  } catch {
    return false;
  }
}

function getUnreadPosts(game: Game | GameRef): number {
  return game.unreadPostsCount ?? 0;
}

function getUnreadComments(game: Game | GameRef): number {
  return game.unreadCommentsCount ?? 0;
}

// A guest is sent totals in the unread fields, so the tooltip must not say unread.
function formatUnreadPostsTooltip(count: number): string {
  return isViewerAuthenticated()
    ? `Непрочитанных постов: ${count}`
    : `Постов: ${count}`;
}

function formatUnreadCommentsTooltip(count: number): string {
  return isViewerAuthenticated()
    ? `Непрочитанных комментариев: ${count}`
    : `Комментариев: ${count}`;
}

function hasUnread(game: Game | GameRef): boolean {
  return getUnreadPosts(game) > 0 || getUnreadComments(game) > 0;
}
