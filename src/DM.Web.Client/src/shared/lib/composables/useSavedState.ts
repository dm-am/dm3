import { computed, ref, type ComputedRef, type Ref } from "vue";

/**
 * Whether a form still matches what was last saved: the submit reads
 * "Сохранено" and is inert until the reader changes anything, with no timer
 * and no toast. Compared by the JSON of what `read` returns.
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
