import { watch, type WatchSource } from "vue";
import { useRoute, useRouter } from "vue-router";

/**
 * A page its reader may not open has no page: while `refused` holds, its
 * address opens the site's "not found" in its place.
 */
export function useNotFoundWhen(refused: WatchSource<boolean>): void {
  const route = useRoute();
  const router = useRouter();

  watch(
    refused,
    (value) => {
      if (!value) return;
      void router.replace({
        name: "not-found",
        params: { pathMatch: route.path.slice(1).split("/") },
        query: route.query,
        hash: route.hash,
      });
    },
    { immediate: true, flush: "post" },
  );
}
