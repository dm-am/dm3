import { ref, onUnmounted } from "vue";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores";
import type { Blog, BlogRef } from "./types";

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
  // Start timestamp refresh when composable is used
  startTimestampRefresh();

  // Stop when component unmounts
  onUnmounted(() => {
    stopTimestampRefresh();
  });

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
   * Автор: Username
   * Ассистент(ы): A, B
   * Читатели: Z
   */
  function buildTooltip(blog: Blog | BlogRef): string {
    const parts: string[] = [];

    // Автор: Username
    if (blog.author?.username) {
      parts.push(`Автор: ${blog.author.username}`);
    }

    // Ассистент(ы): Username, ... (if any)
    const assistants = blog.assistants?.filter((a) => a?.username) ?? [];
    if (assistants.length > 0) {
      const label = assistants.length === 1 ? "Ассистент" : "Ассистенты";
      parts.push(`${label}: ${assistants.map((a) => a.username).join(", ")}`);
    }

    // Читатели: Z
    const totalReaders = blog.subscribersCount ?? 0;
    parts.push(`Читатели: ${totalReaders}`);

    return parts.join("\n");
  }

  /**
   * Format status for display (short form, unified with games)
   */
  function formatStatus(blog: Blog | BlogRef): string {
    if (blog.status === "Draft") return "Оформляется";
    if (blog.status === "Active") return "Открыт";
    if (blog.status === "Closed") return "Закрыт";
    return String(blog.status);
  }

  /**
   * Get status CSS class for styling
   */
  function getStatusClass(blog: Blog | BlogRef): string {
    if (blog.status === "Draft") return "draft";
    if (blog.status === "Active") return "active";
    if (blog.status === "Closed") return "closed";
    return "";
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
   * Check if blog has any unread content
   */
  function hasUnread(blog: Blog | BlogRef): boolean {
    return getUnreadPublications(blog) > 0 || getUnreadComments(blog) > 0;
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
    formatStatus,
    getStatusClass,
    getUnreadPublications,
    getUnreadComments,
    formatUnreadPublicationsTooltip,
    formatUnreadCommentsTooltip,
    hasUnread,
    isNew,
  };
}
