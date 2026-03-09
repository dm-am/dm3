import { ref } from "vue";

/**
 * Composable for managing loading and error states of async operations.
 * Replaces manual boolean flags with a unified pattern.
 */
export function useAsyncAction() {
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
