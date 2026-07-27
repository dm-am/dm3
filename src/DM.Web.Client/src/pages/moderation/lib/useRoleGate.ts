// Client-side role gate for the moderation pages. The router only knows
// requiresAuth; role checks live in the pages themselves (the backend
// enforces roles again with [RequireRole], this is purely presentational).
import { computed } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/shared/stores";
import {
  userIsAdmin,
  userIsModerator,
  userIsSeniorModerator,
} from "@/entities/user";

export type RequiredRole = "Moderator" | "SeniorModerator" | "Admin";

export function useRoleGate(required: RequiredRole = "Moderator") {
  const { user } = storeToRefs(useUserStore());

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

  return { user, hasAccess, isModerator, isSeniorModerator, isAdmin };
}
