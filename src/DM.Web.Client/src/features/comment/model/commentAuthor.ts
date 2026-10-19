import { computed } from "vue";
import type { Comment } from "@/shared/api/models/common/comment";
import { REMOVED_ACCOUNT_NAME, removedAccountRoute } from "@/entities/user";
import { getRoleBadge } from "@/shared/config/roles";
import { isUserOnline } from "@/shared/lib/utils/chat";

export const GUEST_NAME = "Гость";

// A removed account's author and a guest's both arrive as null.
export function commentAuthor(comment: () => Comment, guest: () => boolean) {
  const online = computed(() =>
    isUserOnline(comment().author?.lastActivityUtc),
  );

  const roleBadge = computed(() => getRoleBadge(comment().author?.role));

  // A removed account leads to the "Аккаунт удален" profile; a guest leads nowhere.
  const to = computed(() => {
    const author = comment().author;
    if (author)
      return { name: "profile", params: { username: author.username } };
    return guest() ? null : removedAccountRoute;
  });

  const alt = computed(
    () =>
      comment().author?.username ??
      (guest() ? GUEST_NAME : REMOVED_ACCOUNT_NAME),
  );

  return { online, roleBadge, to, alt };
}
