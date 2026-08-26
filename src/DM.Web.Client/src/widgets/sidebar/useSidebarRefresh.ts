import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

/**
 * The three moments a sidebar list refetches itself.
 *
 * Every block that shows a cached list with per-viewer unread counters wants
 * the same three, and each one is there for its own reason:
 *
 * - on mount, the first read;
 * - on a change of viewer, forced, because a plain fetch() no-ops inside the
 *   cache TTL and the counters belong to whoever is signed in. The sidebar
 *   mounts these blocks once and never unmounts them, so onMounted alone fired
 *   once per page load: a second tab signing another account in left the
 *   previous viewer's counters on the rows for the rest of the session. See
 *   useViewerChange for why "logged in or out" was the wrong question;
 * - on navigation, unforced, so a failed fetch gets another chance once the
 *   TTL cache considers it stale.
 *
 * A block whose fetch is conditional (the guest-only list) keeps its own
 * wiring: the condition belongs with the block that has it, not in a flag here.
 *
 * @param fetch The store action for this block's list.
 */
export function useSidebarRefresh(fetch: (force?: boolean) => void): void {
  const route = useRoute();

  onMounted(() => fetch());

  useViewerChange(() => fetch(true));

  watch(
    () => route.fullPath,
    () => fetch(),
  );
}
