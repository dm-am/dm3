import { onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

/** Forced on a change of viewer: a plain fetch no-ops inside the TTL and the counters are the viewer's; navigation retries a failed fetch. */
export function useSidebarRefresh(fetch: (force?: boolean) => void): void {
  const route = useRoute();

  onMounted(() => fetch());

  useViewerChange(() => fetch(true));

  watch(
    () => route.fullPath,
    () => fetch(),
  );
}
