/**
 * Ending a session while standing on a page that required one.
 *
 * The route guard runs on navigation, and signing out is not one: the viewer
 * pressed "Выйти" on /account and stayed there, on a page whose entire body is
 * `v-if="user"` — a heading over nothing, with no sentence and no way on.
 * Leaving is part of the action.
 *
 * Two conditions, both of them load-bearing. Only a page that needed the
 * session is left: signing out while reading a public topic must not move
 * anyone. And only a session the server confirms is over — session.ts drops the
 * viewer on a confirmed answer and reports false otherwise, so a refused
 * sign-out leaves the page alone as well.
 */
import { useRoute, useRouter } from "vue-router";
import { signOut, signOutAll } from "@/entities/user";

export function useSessionExit() {
  const route = useRoute();
  const router = useRouter();

  async function leaveProtectedPage(ended: boolean): Promise<void> {
    if (!ended || !route.meta.requiresAuth) return;
    await router.push({ name: "home" });
  }

  return {
    signOut: async () => leaveProtectedPage(await signOut()),
    signOutAll: async () => leaveProtectedPage(await signOutAll()),
  };
}
