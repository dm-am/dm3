import { ref, type Ref } from "vue";
import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

/** @param failure What the page says when the list did not come. */
export function createListSlice<T>(
  fetch: (moduleId: string) => Promise<ApiResult<ListEnvelope<T>>>,
  failure: string,
) {
  const items = ref([]) as Ref<T[]>;
  const loading = ref(false);
  const error = ref<string | null>(null);
  const guard = createRequestGuard();

  async function load(moduleId: string): Promise<void> {
    const requestId = guard.next();
    loading.value = true;
    error.value = null;

    const { data, error: failed } = await fetch(moduleId);

    if (!guard.isCurrent(requestId)) return;

    if (failed) {
      error.value = failure;
      items.value = [];
    } else if (data) {
      items.value = data.resources;
    }

    loading.value = false;
  }

  function reset(): void {
    items.value = [];
    loading.value = false;
    error.value = null;
  }

  return { items, loading, error, guard, load, reset };
}
