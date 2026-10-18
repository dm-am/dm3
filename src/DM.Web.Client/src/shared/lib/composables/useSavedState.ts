import { computed, ref, type ComputedRef, type Ref } from "vue";

/**
 * Whether a form on screen still matches what was last saved from it.
 *
 * A settings form has nowhere to show that saving worked. The values in the
 * fields are the ones the reader typed, the server answers with the same
 * values, and the screen after a save is pixel for pixel the screen before
 * it. Every such form used to answer with a toast, which is a message in the
 * corner about something the form itself should be able to say.
 *
 * This is what it says instead: the submit button reads "Сохранено" and is
 * inert while the form and the saved copy agree, and goes back to "Сохранить"
 * the moment the reader changes anything. No timer and no flash - a flash is
 * gone by the time a reader looks up from the field they were editing, and it
 * leaves nothing behind for the one who looks a minute later. The state is
 * the truth about the form, so it can be read at any moment.
 *
 * Comparison is by the JSON of what `read` returns, which is what the form
 * sends anyway. That makes an unchanged form equal to its saved copy whatever
 * order the fields were touched in, and it costs one serialisation per render
 * of a form small enough to fit on a screen.
 */
export interface SavedState {
  /** True while the form matches the copy the server has. */
  saved: ComputedRef<boolean>;
  /** Called after the server confirms a save. */
  remember: () => void;
  /** Label for the submit button, so every form spells it the same way. */
  label: ComputedRef<string>;
}

export function useSavedState(read: () => unknown): SavedState {
  // Null until the first save of this visit: a form the reader has not saved
  // yet is not "saved", however little they have typed in it.
  const stored: Ref<string | null> = ref(null);

  const saved = computed(
    () => stored.value !== null && stored.value === JSON.stringify(read()),
  );

  return {
    saved,
    remember: () => {
      stored.value = JSON.stringify(read());
    },
    label: computed(() => (saved.value ? "Сохранено" : "Сохранить")),
  };
}
