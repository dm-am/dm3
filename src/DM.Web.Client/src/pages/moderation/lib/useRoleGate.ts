// Client-side role gate for the moderation pages. The router only knows
// requiresAuth; role checks live in the pages themselves (the backend
// enforces roles again with [RequireRole], this is purely presentational).
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import {
  userIsAdmin,
  userIsModerator,
  userIsSeniorModerator,
} from "@/entities/user";

export type RequiredRole = "Moderator" | "SeniorModerator" | "Admin";

/**
 * Who the page is for, in the words the refusal uses. The sentence below stood
 * in twelve copies and was missing from seven pages entirely, where a 403 came
 * out as "Не удалось загрузить данные" — a load failure that never happened.
 */
const AUDIENCE: Record<RequiredRole, string> = {
  Moderator: "модераторам",
  SeniorModerator: "старшим модераторам",
  Admin: "администраторам",
};

export function useRoleGate(required: RequiredRole = "Moderator") {
  const { user } = storeToRefs(useAuthStore());

  /** What a viewer without the role reads instead of the page. */
  const deniedText = `Страница доступна только ${AUDIENCE[required]}`;

  const hasAccess = computed(() => {
    switch (required) {
      case "Admin":
        return userIsAdmin(user.value);
      case "SeniorModerator":
        return userIsSeniorModerator(user.value);
      default:
        return userIsModerator(user.value);
    }
  });

  const isModerator = computed(() => userIsModerator(user.value));
  const isSeniorModerator = computed(() => userIsSeniorModerator(user.value));
  const isAdmin = computed(() => userIsAdmin(user.value));

  return {
    user,
    hasAccess,
    deniedText,
    isModerator,
    isSeniorModerator,
    isAdmin,
  };
}
