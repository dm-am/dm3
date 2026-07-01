/**
 * Composable for message edit/delete permissions.
 *
 * Universal — works for global chat, private messages, forum comments.
 * Rule: moderators can always edit/delete; authors can within time limit.
 */

import { computed, type Ref, type ComputedRef } from "vue";
import dayjs from "dayjs";
import type { Message } from "@/shared/api/models/common/message";
import { UserRole } from "@/entities/user/model/types";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";

/** Accepts both UserRef (.role) and full User (.roles) */
interface UserWithRole {
  username: string;
  role?: UserRole;
  roles?: UserRole[];
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
  const MODERATOR_ROLES = new Set([
    UserRole.Admin,
    UserRole.SeniorModerator,
    UserRole.Moderator,
  ]);

  const isModerator = computed(() => {
    if (!user.value) return false;
    // Check both .role (UserRef) and .roles (full User)
    if (user.value.role && MODERATOR_ROLES.has(user.value.role)) return true;
    return user.value.roles?.some((r) => MODERATOR_ROLES.has(r)) ?? false;
  });

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
