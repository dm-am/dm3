/**
 * Composable for message edit/delete permissions.
 *
 * Universal — works for global chat, private messages, forum comments.
 * Rule: moderators can always edit/delete; authors can within time limit.
 */

import { computed, type Ref, type ComputedRef } from "vue";
import dayjs from "dayjs";
import type { Message } from "@/shared/api/models/common/message";
import type { UserRole } from "../model/types";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";
import { userIsModerator } from "./helpers";

/** Minimal user shape: any DTO carrying the single site role (UserRef, User). */
interface UserWithRole {
  username: string;
  role: UserRole;
}

export interface MessagePermissions {
  isModerator: ComputedRef<boolean>;
  canEdit: (msg: Message) => boolean;
  canDelete: (msg: Message) => boolean;
  canLike: (msg: Message) => boolean;
}

export function useMessagePermissions(
  user: Ref<UserWithRole | null>,
  editTimeLimitMinutes = EDIT_TIME_LIMIT_MINUTES,
): MessagePermissions {
  const isModerator = computed(() => userIsModerator(user.value));

  function isWithinTimeLimit(createdUtc: string): boolean {
    return (
      dayjs().diff(dayjs(createdUtc), "minute", true) <= editTimeLimitMinutes
    );
  }

  function canEdit(msg: Message): boolean {
    if (!user.value || msg.isRemoved) return false;
    if (isModerator.value) return true;
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

  return { isModerator, canEdit, canDelete, canLike };
}
