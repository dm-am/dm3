import { watch, nextTick, type Ref } from "vue";
import { useRoute, useRouter } from "vue-router";

/**
 * Composable for scrolling to a specific element based on query parameter
 * @param itemsLoaded - Ref indicating when items are loaded and ready for scroll
 */
export function useScrollToElement(itemsLoaded: Ref<boolean>) {
  const route = useRoute();
  const router = useRouter();

  async function scrollToTarget() {
    const targetId = route.query.scrollTo as string;
    if (!targetId) return;

    await nextTick();
    // Wait a bit for DOM to fully render
    await new Promise((r) => setTimeout(r, 100));

    const element = document.querySelector(`[data-id="${targetId}"]`);
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "center" });
      element.classList.add("highlight-unread");
      setTimeout(() => element.classList.remove("highlight-unread"), 2000);

      // Remove scrollTo from URL to prevent re-scrolling on navigation
      const { scrollTo, ...restQuery } = route.query;
      router.replace({ ...route, query: restQuery });
    }
  }

  watch(
    itemsLoaded,
    (loaded) => {
      if (loaded && route.query.scrollTo) {
        scrollToTarget();
      }
    },
    { immediate: true },
  );
}
