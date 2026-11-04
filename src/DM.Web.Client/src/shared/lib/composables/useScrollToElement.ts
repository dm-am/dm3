import { watch, nextTick, type Ref } from "vue";
import { useRoute, useRouter } from "vue-router";

function reveal(element: Element): void {
  element.scrollIntoView({ behavior: "smooth", block: "center" });
  markArrival(element);
}

/**
 * The unread highlight for the length of its pulse in Reset.sass: a mark that
 * stays would say something else than "this is the one you asked for".
 */
export function markArrival(element: Element): void {
  element.classList.add("highlight-unread");
  setTimeout(() => element.classList.remove("highlight-unread"), 2000);
}

/** After the render, and a moment for avatars and BBCode media to settle. */
async function settleLayout(): Promise<void> {
  await nextTick();
  await new Promise((resolve) => setTimeout(resolve, 100));
}

/**
 * Scrolls to the element `?scrollTo=` names once the items are loaded.
 * @param itemsLoaded - Ref indicating when items are loaded and ready for scroll
 */
export function useScrollToElement(itemsLoaded: Ref<boolean>) {
  const route = useRoute();
  const router = useRouter();

  async function scrollToTarget() {
    const targetId = String(route.query.scrollTo ?? "");
    if (!targetId) return;

    await settleLayout();

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
 * Scrolls to the comment of the URL hash (#comment-{id}) once its page has
 * rendered: the link a comment's number and a warning quote are.
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

    await settleLayout();

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
