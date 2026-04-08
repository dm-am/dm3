import { ref, type Ref } from "vue";

/**
 * State returned by useAsyncAction
 */
export type AsyncActionState = {
  loading: Ref<boolean>;
  error: Ref<string | null>;
  execute: (fn: () => Promise<void>) => Promise<void>;
  clearError: () => void;
};

/**
 * Composable for managing loading and error states of async operations.
 * Replaces manual boolean flags with a unified pattern.
 */
export function useAsyncAction(): AsyncActionState {
  const loading = ref(false);
  const error = ref<string | null>(null);

  async function execute(fn: () => Promise<void>) {
    loading.value = true;
    error.value = null;
    try {
      await fn();
    } catch (e: unknown) {
      if (e instanceof Error) {
        error.value = e.message;
      } else if (typeof e === "string") {
        error.value = e;
      } else {
        error.value = "Произошла неизвестная ошибка";
      }
    } finally {
      loading.value = false;
    }
  }

  function clearError() {
    error.value = null;
  }

  return { loading, error, execute, clearError };
}
