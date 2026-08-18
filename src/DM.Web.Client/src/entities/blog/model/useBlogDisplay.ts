import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import { useAuthStore } from "@/shared/stores";
import type { Blog, BlogRef } from "./types";

/**
 * Auth state for counter tooltip wording. Resolved lazily (only when a
 * counter tooltip is built) so the composable keeps working in contexts
 * without an active Pinia instance; guest wording is the safe default.
 * Reading the store inside render/computed keeps the value reactive.
 *
 * For guests the backend intentionally returns total counts in the
 * unread fields, so counter tooltips must not say "unread" for them.
 */
function isViewerAuthenticated(): boolean {
  try {
    return useAuthStore().isAuthenticated;
  } catch {
    return false;
  }
}

/**
 * Unified composable for blog display logic
 * Single source of truth for tooltips, counters, and formatted info
 */
export function useBlogDisplay() {
  // Cached timestamp for isNew(); shared single timer, see useNowTimestamp
  const nowTimestamp = useNowTimestamp();

  /**
   * Build status tooltip with multiple dates based on status (per GLOSSARY.md):
   * - Draft: creation date
   * - Active: creation date + opening date
   * - Closed: creation date + opening date + close date
   */
  function buildStatusTooltip(blog: Blog | BlogRef): string {
    const lines: string[] = [];

    // Creation date - always shown
    lines.push(`Создание блога: ${formatDateFull(blog.createdUtc)}`);

    // Opening date - for Active and Closed
    if (blog.status === "Active" || blog.status === "Closed") {
      lines.push(`Открытие блога: ${formatDateFull(blog.activatedUtc)}`);
    }

    // Close date - only for Closed
    if (blog.status === "Closed") {
      lines.push(`Закрытие блога: ${formatDateFull(blog.closedUtc)}`);
    }

    return lines.join("\n");
  }

  /**
   * Build blog tooltip with labels (multiline):
   *   "Автор: Username"
   *   "Ассистент: A" / "Ассистенты: A, B"
   *   "Читатели: Z"
   */
  function buildTooltip(blog: Blog | BlogRef): string {
    const parts: string[] = [];

    // "Автор: {username}"
    if (blog.author?.username) {
      parts.push(`Автор: ${blog.author.username}`);
    }

    // "Ассистент" / "Ассистенты": Username, ... (if any)
    const assistants = blog.assistants?.filter((a) => a?.username) ?? [];
    if (assistants.length > 0) {
      const label = assistants.length === 1 ? "Ассистент" : "Ассистенты";
      parts.push(`${label}: ${assistants.map((a) => a.username).join(", ")}`);
    }

    // "Читатели: {N}"
    const totalReaders = blog.subscribersCount ?? 0;
    parts.push(`Читатели: ${totalReaders}`);

    return parts.join("\n");
  }

  /**
   * Build assistant(s) tooltip: "Ассистент: X" / "Ассистенты: X, Y".
   * Unified with games (see useGameDisplay.buildAssistantTooltip).
   */
  function buildAssistantTooltip(assistants: { username: string }[]): string {
    const names = assistants.map((a) => a.username).join(", ");
    return `Ассистент${assistants.length > 1 ? "ы" : ""}: ${names}`;
  }

  /**
   * Get unread publications count with null safety
   */
  function getUnreadPublications(blog: Blog | BlogRef): number {
    return blog.unreadPublicationsCount ?? 0;
  }

  /**
   * Get unread comments count with null safety (blog + all publications)
   */
  function getUnreadComments(blog: Blog | BlogRef): number {
    return blog.unreadCommentsCount ?? 0;
  }

  /**
   * Format publications counter tooltip.
   * Authenticated: "Непрочитанных публикаций: 5"
   * Guest (counts are totals): "Публикаций: 5"
   */
  function formatUnreadPublicationsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных публикаций: ${count}`
      : `Публикаций: ${count}`;
  }

  /**
   * Format comments counter tooltip.
   * Authenticated: "Непрочитанных комментариев: 3"
   * Guest (counts are totals): "Комментариев: 3"
   */
  function formatUnreadCommentsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных комментариев: ${count}`
      : `Комментариев: ${count}`;
  }

  /**
   * Check if blog was activated less than 7 days ago
   * Used to highlight "new" blogs with green color
   * Optimized: uses cached timestamp instead of creating Date per call
   */
  function isNew(blog: Blog | BlogRef): boolean {
    if (!blog.activatedUtc) return false;
    const activatedMs = new Date(blog.activatedUtc).getTime();
    const diffMs = nowTimestamp.value - activatedMs;
    const sevenDaysMs = 7 * 24 * 60 * 60 * 1000;
    return diffMs < sevenDaysMs;
  }

  return {
    buildTooltip,
    buildStatusTooltip,
    buildAssistantTooltip,
    getUnreadPublications,
    getUnreadComments,
    formatUnreadPublicationsTooltip,
    formatUnreadCommentsTooltip,
    isNew,
  };
}
