import { ref, type Ref } from "vue";
import type { ApiResult, Envelope } from "@/shared/api/models/common";

/**
 * The one ticket a page shows, as `read` answers it. A 404 is `notFound`, not
 * an error: each page says what it stands for. Any other refusal is
 * `loadError`, which ErrorState offers to retry.
 */
export function useTicketRead<T>(read: () => Promise<ApiResult<Envelope<T>>>) {
  const ticket = ref<T | null>(null) as Ref<T | null>;
  const loading = ref(false);
  const loadError = ref<string | null>(null);
  const notFound = ref(false);

  async function load() {
    loading.value = true;
    notFound.value = false;
    const { data, error } = await read();
    loading.value = false;
    if (error) {
      if (error.status === 404) {
        notFound.value = true;
        ticket.value = null;
        loadError.value = null;
        return;
      }
      loadError.value = "Не удалось загрузить обращение";
      return;
    }
    loadError.value = null;
    ticket.value = data?.resource ?? null;
    notFound.value = ticket.value === null;
  }

  return { ticket, loading, loadError, notFound, load };
}
