import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import { useAuthStore } from "@/shared/stores";
import { ModuleStatus } from "@/shared/api/models/common";
import type { Blog, BlogRef } from "./types";

/** A blog as the "new" mark reads it; a subscription row carries the date alone. */
type Dated = Pick<Blog | BlogRef, "activatedUtc"> &
  Partial<Pick<Blog, "status">>;

// A guest is sent totals in the unread fields, so the tooltip must not say unread.
function isViewerAuthenticated(): boolean {
  // Without an installed pinia the guest wording is the safe answer.
  try {
    return useAuthStore().isAuthenticated;
  } catch {
    return false;
  }
}

export function useBlogDisplay() {
  const nowTimestamp = useNowTimestamp();

  function buildStatusTooltip(blog: Blog | BlogRef): string {
    const lines: string[] = [];

    lines.push(`Создание блога: ${formatDateFull(blog.createdUtc)}`);

    if (blog.status === "Active" || blog.status === "Closed") {
      lines.push(`Открытие блога: ${formatDateFull(blog.activatedUtc)}`);
    }

    if (blog.status === "Closed") {
      lines.push(`Закрытие блога: ${formatDateFull(blog.closedUtc)}`);
    }

    return lines.join("\n");
  }

  function buildTooltip(blog: Blog | BlogRef): string {
    const parts: string[] = [];

    if (blog.author?.username) {
      parts.push(`Автор: ${blog.author.username}`);
    }

    const assistants = blog.assistants?.filter((a) => a?.username) ?? [];
    if (assistants.length > 0) {
      const label = assistants.length === 1 ? "Ассистент" : "Ассистенты";
      parts.push(`${label}: ${assistants.map((a) => a.username).join(", ")}`);
    }

    const totalReaders = blog.subscribersCount ?? 0;
    parts.push(`Читатели: ${totalReaders}`);

    return parts.join("\n");
  }

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

  function formatUnreadPublicationsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных публикаций: ${count}`
      : `Публикаций: ${count}`;
  }

  function formatUnreadCommentsTooltip(count: number): string {
    return isViewerAuthenticated()
      ? `Непрочитанных комментариев: ${count}`
      : `Комментариев: ${count}`;
  }

  /** Opened less than seven days ago, and not closed: a closed blog never gets the green. */
  function isNew(blog: Dated): boolean {
    if (!blog.activatedUtc || blog.status === ModuleStatus.Closed) return false;
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
