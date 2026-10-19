// Signing out is no navigation, so the guard does not run: leave a page that needed the session.
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
