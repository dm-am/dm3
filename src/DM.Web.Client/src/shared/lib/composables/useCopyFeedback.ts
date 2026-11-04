import { computed, onUnmounted, ref, type ComputedRef, type Ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";

/**
 * How a control answers for what it put in the clipboard: it says so itself
 * for two seconds, in words or with the mark of the outcome, since the reader
 * is looking at it and not at a plate in the corner.
 */

/** A control with a caption says this instead of its own word. */
export const COPIED = "Скопировано";

/** And this when the clipboard refused. */
export const COPY_FAILED = "Не удалось";

/** How long the answer stands before the control goes back to itself. */
export const HOLD_MS = 2000;

type CopyOutcome = "idle" | "copied" | "failed";

export interface CopyFeedback {
  /** What the control is saying right now. */
  outcome: Readonly<Ref<CopyOutcome>>;
  /** Writes to the clipboard and makes the control answer. */
  copy: (text: string) => Promise<boolean>;
  /** The caption of a control that has one, given its resting word. */
  label: (resting: string) => ComputedRef<string>;
  /** The mark for a control that has no caption; null while at rest. */
  mark: ComputedRef<string | null>;
}

export function useCopyFeedback(): CopyFeedback {
  const outcome = ref<CopyOutcome>("idle");
  let timer: ReturnType<typeof setTimeout> | null = null;

  async function copy(text: string): Promise<boolean> {
    let written = true;
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      // Every failure is the same fact for the reader: nothing in the clipboard.
      written = false;
    }

    outcome.value = written ? "copied" : "failed";
    // A second press while the first answer still stands restarts the two
    // seconds rather than stacking a second timer on the same control.
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      outcome.value = "idle";
      timer = null;
    }, HOLD_MS);

    return written;
  }

  onUnmounted(() => {
    if (timer) clearTimeout(timer);
  });

  return {
    outcome,
    copy,
    label: (resting: string) =>
      computed(() =>
        outcome.value === "copied"
          ? COPIED
          : outcome.value === "failed"
            ? COPY_FAILED
            : resting,
      ),
    mark: computed(() =>
      outcome.value === "copied"
        ? symbols.checkmark
        : outcome.value === "failed"
          ? symbols.cross
          : null,
    ),
  };
}
