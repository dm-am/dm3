import { ref, type Ref } from "vue";
import type { ApiResult, GeneralError } from "@/shared/api/models/common";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { refusalOfTheReader, takeOverRefusal } from "@/shared/lib/errors";

/**
 * The loading and error refs and the race guard of one request a screen runs
 * on its own; `error` is null or the exact text for ErrorState.
 *
 * @example
 * ```ts
 * const {
 *   loading,
 *   error: loadError,
 *   run,
 * } = useGuardedRequest({ message: "Не удалось загрузить топики" });
 *
 * const envelope = ref<ListEnvelope<Topic> | null>(null);
 *
 * function fetchTopics() {
 *   return run(
 *     () => forumApi.getAllTopics(apiQuery.value),
 *     (data) => { envelope.value = data; },
 *   );
 * }
 * ```
 */
export interface UseGuardedRequestOptions {
  /** The text of a failure, or a function of it (describeFailure). */
  message: string | ((error: GeneralError) => string);
  /**
   * Whether a retry hides the previous failure before its answer lands. The
   * screens disagree visibly, so the choice stays at the call site. Default
   * false: the failure stands until the next answer.
   */
  clearErrorOnStart?: boolean;
}

export interface UseGuardedRequestReturn {
  /** True from the moment `run` is called until its own answer lands. */
  loading: Ref<boolean>;
  /** The failure text for `ErrorState`, or null. */
  error: Ref<string | null>;
  /**
   * Runs one request; `apply` gets only the newest answer that did not fail,
   * and a superseded one does not even lower `loading`.
   */
  run: <T>(
    fetcher: () => Promise<ApiResult<T>>,
    apply: (data: T | null) => void,
  ) => Promise<void>;
  /** Drops the failure text without starting anything — a scope change. */
  clearError: () => void;
}

/**
 * Takes down the held plate of a failed load whose screen says the failure in
 * its own error box, and hands the answer on (refusalOfTheReader keeps theirs).
 * A fetcher of two requests passes each answer through as it arrives.
 */
export function shownOnPage<R extends { error?: GeneralError | null }>(
  result: R,
): R {
  const failure = result.error;
  if (failure && !refusalOfTheReader(failure.status)) takeOverRefusal(failure);
  return result;
}

export function useGuardedRequest(
  options: UseGuardedRequestOptions,
): UseGuardedRequestReturn {
  const { message, clearErrorOnStart = false } = options;

  const loading = ref(false);
  const error = ref<string | null>(null);

  // Whichever reply lands last would win, showing a page the reader has left.
  const guard = createRequestGuard();

  function describe(failure: GeneralError): string {
    return typeof message === "function" ? message(failure) : message;
  }

  async function run<T>(
    fetcher: () => Promise<ApiResult<T>>,
    apply: (data: T | null) => void,
  ): Promise<void> {
    const requestId = guard.next();
    loading.value = true;
    if (clearErrorOnStart) error.value = null;

    try {
      const { data, error: failure } = shownOnPage(await fetcher());
      // The newer request speaks for the screen now.
      if (!guard.isCurrent(requestId)) return;

      // Before apply: the watch it may trigger starts the request that owns loading.
      loading.value = false;

      if (failure) {
        // The error box renders beside the content, not instead of it.
        error.value = describe(failure);
        return;
      }

      error.value = null;
      apply(data ?? null);
    } finally {
      // A throwing fetcher still lowers the flag; the throw is not swallowed.
      if (guard.isCurrent(requestId)) loading.value = false;
    }
  }

  function clearError(): void {
    error.value = null;
  }

  return { loading, error, run, clearError };
}
