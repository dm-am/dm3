/**
 * The skeleton length of a viewer's own block: the lines the block took on
 * screen at his last visit in this browser, a wrapped title two of them, so the
 * column below it does not jump when the list arrives. A browser that has never
 * seen the block, or cannot keep the number, falls back to the block's fixed
 * count.
 */
import { nextTick, ref, watch, type Ref } from "vue";
import { useAuthStore } from "@/entities/user";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";

// A guard against a corrupt value, not a bound of the list: a longer list only
// shows a shorter skeleton.
const MAX_LINES = 1000;

// A skeleton row is 1.3em of the list's font (SidebarSkeleton).
const SKELETON_LINE_HEIGHT = 1.3;

const keyOf = (block: string, username: string) =>
  `dm_sidebar_lines:${block}:${username}`;

function readLines(key: string): number | null {
  try {
    const raw = localStorage.getItem(key);
    if (raw === null) return null;
    const lines = Number(raw);
    return Number.isInteger(lines) && lines >= 1 && lines <= MAX_LINES
      ? lines
      : null;
  } catch {
    return null;
  }
}

function storeLines(key: string, lines: number): void {
  try {
    localStorage.setItem(key, String(lines));
  } catch {
    // A browser that keeps nothing gets the fixed count next time.
  }
}

/**
 * The skeleton rows the list's rows fill on screen, from the first row to the
 * item after the last one. Null where nothing is laid out: a hidden block, a
 * DOM without layout.
 */
function linesOnScreen(rowsEnd: HTMLElement): number | null {
  const list = rowsEnd.parentElement;
  const first = list?.firstElementChild;
  if (!list || !first || first === rowsEnd) return null;
  const height =
    rowsEnd.getBoundingClientRect().top - first.getBoundingClientRect().top;
  const line =
    parseFloat(getComputedStyle(list).fontSize) * SKELETON_LINE_HEIGHT;
  return height > 0 && line > 0 ? Math.round(height / line) : null;
}

/**
 * @param block The block's key, one per block.
 * @param fallback The skeleton length when nothing is remembered.
 * @param rows The rows the block draws now, null until its list is read.
 * @param rowsEnd The item right after the list's rows, the block's own.
 */
export function useRememberedLines(
  block: string,
  fallback: number,
  rows: () => number | null,
  rowsEnd: Readonly<Ref<HTMLElement | null>>,
): Ref<number> {
  const auth = useAuthStore();
  const lines = ref(fallback);

  // Per viewer: another account in the same browser has another list.
  const readFor = (username: string | undefined) => {
    lines.value =
      (username ? readLines(keyOf(block, username)) : null) ?? fallback;
  };
  readFor(auth.user?.username);
  useViewerChange(readFor);

  // Immediate: a list already in memory from an earlier page is a visit too.
  watch(
    rows,
    (count) => {
      const username = auth.user?.username;
      if (count === null || !username) return;
      const keep = (drawn: number) => {
        // An empty list still draws its one line of "nothing yet".
        lines.value = Math.min(Math.max(drawn, 1), MAX_LINES);
        storeLines(keyOf(block, username), lines.value);
      };
      // The row count until the rows are laid out, then the lines they took.
      keep(count);
      void nextTick(() => {
        const onScreen = rowsEnd.value && linesOnScreen(rowsEnd.value);
        if (onScreen) keep(onScreen);
      });
    },
    { immediate: true },
  );

  return lines;
}
