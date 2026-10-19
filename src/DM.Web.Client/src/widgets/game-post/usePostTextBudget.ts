import { nextTick, onMounted, onUnmounted, ref, type Ref } from "vue";

/** The budget until the column is laid out. */
const FALLBACK = 150;

/** How much of a long post shows before "показать полностью": the lines beside the author's column. */
export function usePostTextBudget(
  column: Ref<HTMLElement | null>,
): Ref<number> {
  const budget = ref(FALLBACK);

  function recalcMaxHeight() {
    if (!column.value) return;
    const metaH = column.value.offsetHeight;
    if (metaH <= 0) return;

    // The line of the button.
    const expandLinkH = 22;
    const available = metaH - expandLinkH;

    // A budget only: TruncatedContent snaps to whole lines itself.
    const lineH = 20;
    const lines = Math.max(1, Math.floor(available / lineH));
    budget.value = lines * lineH;
  }

  let resizeObserver: ResizeObserver | null = null;

  onMounted(() => {
    nextTick(recalcMaxHeight);
    if (column.value) {
      resizeObserver = new ResizeObserver(recalcMaxHeight);
      resizeObserver.observe(column.value);
    }
  });

  onUnmounted(() => {
    resizeObserver?.disconnect();
  });

  return budget;
}
