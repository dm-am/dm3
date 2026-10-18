import { computed, onUnmounted, ref, type ComputedRef, type Ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";

/**
 * How a control answers for what it put in the clipboard.
 *
 * The clipboard is invisible, so copying is one of the few actions whose
 * result the screen genuinely cannot show - and for a long time every one of
 * them answered with a plate in the corner. A plate is the wrong carrier here
 * for a reason that has nothing to do with taste: the reader is looking at the
 * control they just pressed, and the answer arrives at the other end of the
 * page. The control itself is where they are looking, and it has room to say
 * two words.
 *
 * One idiom for every copy on the site, so the answer reads the same wherever
 * it comes from: the control says so for two seconds and goes back. Controls
 * with a caption say it in words, controls without one - the anchor icon in
 * the chat toolbar, the number of a comment - say it with the mark of the same
 * two outcomes. The two seconds are the ones the bot-link button already held.
 */

/** A control with a caption says this instead of its own word. */
export const COPIED = "Скопировано";

/** And this when the clipboard refused. */
export const COPY_FAILED = "Не удалось";

/** How long the answer stands before the control goes back to itself. */
export const HOLD_MS = 2000;

export type CopyOutcome = "idle" | "copied" | "failed";

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
      // Every way this fails is the same fact for the reader: what they wanted
      // is not in the clipboard. The page is not on https, the tab lost focus,
      // the browser withheld permission, or there is no clipboard API at all.
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
