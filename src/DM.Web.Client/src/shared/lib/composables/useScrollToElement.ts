import { watch, nextTick, type Ref } from "vue";
import { useRoute, useRouter } from "vue-router";

/**
 * Bring an element into view and mark it for a moment.
 *
 * The mark is the same class the unread highlight uses, and it comes off after
 * two seconds: what it says is "this is the one you asked for", and a highlight
 * that stays says something else.
 */
function reveal(element: Element): void {
  element.scrollIntoView({ behavior: "smooth", block: "center" });
  element.classList.add("highlight-unread");
  setTimeout(() => element.classList.remove("highlight-unread"), 2000);
}

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
      reveal(element);

      // Remove scrollTo from URL to prevent re-scrolling on navigation
      const restQuery = { ...route.query };
      delete restQuery.scrollTo;
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

/**
 * Scroll to the comment named by the URL hash (#comment-{id}) once the page
 * holding it has rendered.
 *
 * Backs the permalink a comment copies. On the game and the blog that link used
 * to open the page and go nowhere, because the handler lived on the topic
 * alone; it was then copied to the discussion widget, which is the same defect
 * one screen further along.
 *
 * @param comments Getter for the rendered list — both what to wait for and the
 * proof that there is anything to look in.
 */
export function useCommentHashScroll(
  comments: () => readonly unknown[] | undefined,
): void {
  const route = useRoute();

  async function scrollToHashComment() {
    const hash = route.hash;
    if (!hash.startsWith("#comment-")) return;
    if (!comments()?.length) return;

    await nextTick();
    // Wait for content (avatars, BBCode media) to settle before measuring.
    await new Promise((resolve) => setTimeout(resolve, 100));

    const element = document.getElementById(hash.slice(1));
    if (!element) return;
    reveal(element);
  }

  watch(
    () => [comments(), route.hash] as const,
    () => scrollToHashComment(),
    { immediate: true, flush: "post" },
  );
}
