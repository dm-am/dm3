/**
 * The address bar of the moderation lists' specs, handed to vi.mock:
 *
 *   vi.mock("vue-router", async () =>
 *     (await import("./moderationLists.doubles")).vueRouter(),
 *   );
 *
 * The query is a ref, because the pages read it inside computeds; the push
 * moves it and, as the real router does, refuses to move to the address
 * already open.
 */
import { shallowRef } from "vue";
import { vi } from "vitest";

export function vueRouter() {
  const query = shallowRef<Record<string, string>>({});
  const isCurrent = (next: Record<string, string>) => {
    const current = query.value;
    const keys = Object.keys(next);
    return (
      keys.length === Object.keys(current).length &&
      keys.every((key) => current[key] === next[key])
    );
  };
  const push = vi.fn((to: { query?: Record<string, string> }) => {
    const next = { ...(to.query ?? {}) };
    if (isCurrent(next)) return;
    query.value = next;
  });
  return {
    useRoute: () => ({
      get query() {
        return query.value;
      },
    }),
    useRouter: () => ({ push }),
    routeQuery: query,
    routerPush: push,
  };
}
