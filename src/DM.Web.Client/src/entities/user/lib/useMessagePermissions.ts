// A moderator always where the chat is moderated (the global one), the author within the time limit.

import { computed, type Ref, type ComputedRef } from "vue";
import dayjs from "dayjs";
import type { Message } from "@/shared/api/models/common/message";
import type { UserRole } from "../model/types";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";
import { userIsModerator } from "./roles";

interface UserWithRole {
  username: string;
  role: UserRole;
}

export interface MessagePermissions {
  isModerator: ComputedRef<boolean>;
  /** Opens a removed message: a moderator where moderation reaches, never in rooms or private chats. */
  revealsRemoved: ComputedRef<boolean>;
  canEdit: (msg: Message) => boolean;
  canDelete: (msg: Message) => boolean;
  canLike: (msg: Message) => boolean;
}

export function useMessagePermissions(
  user: Ref<UserWithRole | null>,
  /** The feed: moderation reaches the chat where it carries any (the global chat alone). */
  feed: { moderation?: unknown },
  editTimeLimitMinutes = EDIT_TIME_LIMIT_MINUTES,
): MessagePermissions {
  const isModerator = computed(() => userIsModerator(user.value));
  const revealsRemoved = computed(() => isModerator.value && !!feed.moderation);

  function isWithinTimeLimit(createdUtc: string): boolean {
    return (
      dayjs().diff(dayjs(createdUtc), "minute", true) <= editTimeLimitMinutes
    );
  }

  function canEdit(msg: Message): boolean {
    if (!user.value || msg.isRemoved) return false;
    if (feed.moderation && isModerator.value) return true;
    if (msg.author?.username !== user.value.username) return false;
    return isWithinTimeLimit(msg.createdUtc);
  }

  function canDelete(msg: Message): boolean {
    return canEdit(msg);
  }

  function canLike(msg: Message): boolean {
    if (!user.value || msg.isRemoved) return false;
    return msg.author?.username !== user.value.username;
  }

  return { isModerator, revealsRemoved, canEdit, canDelete, canLike };
}
