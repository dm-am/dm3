import { ref, type Ref } from "vue";
import type { ApiResult, GeneralError } from "@/shared/api/models/common";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { refusalOfTheReader, takeOverRefusal } from "@/shared/lib/errors";

/**
 * The three refs a screen needs to run one request of its own, in one place.
 *
 * Not every list belongs in a store: a profile subpage, a scoped widget or a
 * leaderboard reads one endpoint for as long as it is mounted and throws the
 * answer away on unmount. Those screens were each writing the same fifteen
 * lines — a `loading` ref, an error ref, a `createRequestGuard()`, and a fetch
 * function that had to remember to check `isCurrent` before every assignment
 * and again before lowering `loading`. Six copies, and the differences between
 * them were spelling plus one genuine choice (see `clearErrorOnStart`).
 *
 * What stays with the caller is the part that is actually local: which endpoint,
 * which params, and where the payload goes. `run` owns the race and the two
 * state flags, nothing else.
 *
 * The counterpart in the template is `ErrorState`: `error` is either null or the
 * exact text to render, so the message lives next to the request that can fail
 * rather than being repeated in the markup.
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
  /**
   * The text `error` carries when the call fails. A function when the message
   * depends on the failure — `describeFailure` names the refusal for the
   * refusals a reader can act on.
   */
  message: string | ((error: GeneralError) => string);
  /**
   * Whether a new attempt hides the previous failure before its answer lands.
   *
   * The option exists because the six screens this replaces disagreed, and the
   * disagreement is visible: with it the banner goes away for the length of the
   * retry, without it the banner stays up until the answer decides. Three
   * screens did each. Picking one for all of them changes what a reader sees,
   * so the choice stays with the screen and is named at every call site until
   * somebody rules on it.
   *
   * Default: false — the failure survives until the next answer, which is what
   * the error-beside-content lists want.
   */
  clearErrorOnStart?: boolean;
}

export interface UseGuardedRequestReturn {
  /** True from the moment `run` is called until its own answer lands. */
  loading: Ref<boolean>;
  /** The failure text for `ErrorState`, or null. */
  error: Ref<string | null>;
  /**
   * Runs one request. `apply` is called only for an answer that is still the
   * newest and did not fail; everything a superseded answer would have written
   * is dropped, including the lowering of `loading`.
   */
  run: <T>(
    fetcher: () => Promise<ApiResult<T>>,
    apply: (data: T | null) => void,
  ) => Promise<void>;
  /** Drops the failure text without starting anything — a scope change. */
  clearError: () => void;
}

/**
 * Takes down the plate the interceptor holds for a failed load whose screen
 * says the failure itself, in the error box beside its content, and hands the
 * answer on unchanged.
 *
 * The owner's rule of 26 September 2026: a plate only where the page has
 * nowhere to say it. Every screen that runs its load here shows `error` in an
 * ErrorState or a line of its own, so the plate in the corner was the same
 * refusal said a second time - "Не удалось загрузить топики [Повторить]" on the
 * page and "Запрос не выполнен: ошибка сервера" beside it. An expired session
 * and a request that never arrived keep their plate: they are about the reader
 * and the network, not about the list (refusalOfTheReader).
 *
 * `run` does this for the failure it is handed. A fetcher that waits on two
 * requests passes each answer through here as it arrives: the plate is held
 * only until the end of the task its answer came in, and the first answer
 * would be up in the corner before the second one lands.
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

  // Discards the answer to a request a newer one has superseded: a fast
  // search/page/scope change puts two requests on the wire, and without this
  // whichever reply lands last wins, so the table can end up showing the page
  // the reader has already left.
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
      // A superseded failure is taken down too: the newer request speaks for
      // the screen now, and a plate about the list the reader has left would
      // stand over the one that loaded.
      if (!guard.isCurrent(requestId)) return;

      // Lowered before `apply`, not after: applying the payload can move state
      // the caller watches, and the next request that watch starts must be the
      // one that owns `loading` from then on.
      loading.value = false;

      if (failure) {
        // The payload the screen already shows stays: the error box renders
        // beside the content, not instead of it.
        error.value = describe(failure);
        return;
      }

      error.value = null;
      apply(data ?? null);
    } finally {
      // A fetcher that throws rather than returning a failure still has to
      // lower the flag; the throw itself is not swallowed.
      if (guard.isCurrent(requestId)) loading.value = false;
    }
  }

  function clearError(): void {
    error.value = null;
  }

  return { loading, error, run, clearError };
}
