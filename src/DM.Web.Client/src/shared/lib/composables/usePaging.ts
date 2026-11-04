import { computed } from "vue";
import { useAuthStore } from "@/shared/stores/auth";

/**
 * The page size of a guest and of a reader who never saved one: the server's
 * PagingPolicy.DefaultPageSize, which PagingContractShould holds this copy to.
 */
export const DEFAULT_PAGE_SIZE = 20;

/**
 * The largest page the API serves (PagingPolicy): a larger take is a validation
 * error, so a saved preference above it would quietly do nothing.
 */
export const MAX_API_PAGE_SIZE = 200;

/** The one page size of every paged list: the reader's, or the default, within what the API serves. */
export function usePaging() {
  const authStore = useAuthStore();

  const pageSize = computed(() =>
    Math.min(
      authStore.user?.settings?.pageSize ?? DEFAULT_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
    ),
  );

  return { pageSize };
}
