import {
  isAxiosError,
  type AxiosError,
  type AxiosRequestConfig,
  type AxiosResponse,
} from "axios";
import type { GeneralError } from "./models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  announceGeneral,
  announceWorded,
  describeFailure,
  describeRateLimit,
  GENERAL_REASONS,
  type GeneralReason,
} from "@/shared/lib/errors";
import { otherSiteAddresses } from "@/shared/config/site";
import { addressAnswers } from "@/shared/lib/composables/useAddressPing";

/** Options beyond axios', riding on the request config to the interceptor. */
export type RequestOptions = {
  /**
   * The caller shows a 403 itself: signing in, where the refusal is the whole
   * answer and belongs under the field.
   */
  ownsRefusal?: boolean;

  /**
   * The caller shows a 429 itself: a form pressed again shows the wait in its
   * own strip. Apart from ownsRefusal, whose callers keep their toast.
   */
  ownsRateLimit?: boolean;

  /**
   * The request asks whether there is a session at all, so a 401 means
   * "nobody", not an expiry.
   */
  probesSession?: boolean;
};

/** A caller that prints its own refusal and its own wait: the interceptor stays quiet. */
export const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true } as const;

/** Request options that put a read past both caches: for a reload after an edit. */
export const FRESH = { headers: { "Cache-Control": "no-cache" } };

/** An axios config with this client's own options riding along on it. */
export type TaggedRequest = AxiosRequestConfig & RequestOptions;

/** The flags a call site sets, in the shape axios carries to the interceptor. */
export const tagged = (options?: RequestOptions): TaggedRequest => ({
  ownsRefusal: options?.ownsRefusal,
  ownsRateLimit: options?.ownsRateLimit,
  probesSession: options?.probesSession,
});

/** Whether the request that failed carries this option. */
function flag(
  request: AxiosRequestConfig | undefined,
  option: keyof RequestOptions,
): boolean {
  return Boolean((request as TaggedRequest | undefined)?.[option]);
}

/**
 * Whole seconds off Retry-After, or undefined when the server named none. The
 * API writes seconds, never the HTTP-date form.
 */
function retryAfterSeconds(response: AxiosResponse): number | undefined {
  const header = response.headers?.["retry-after"];
  if (!header) return undefined;

  const seconds = Number(header);
  return Number.isFinite(seconds) && seconds > 0
    ? Math.ceil(seconds)
    : undefined;
}

/**
 * Installed by the app at startup: shared may not know the router. Until then
 * an expired session only shows the toast.
 */
let onSessionExpired: (() => void) | null = null;

/** Installed once by the app layer, which owns navigation. */
export function setSessionExpiredHandler(handler: () => void): void {
  onSessionExpired = handler;
}

/** Names the site: "this address" alone said nothing to a reader of one address. */
const UNREACHABLE = "Сайт не отвечает по этому адресу";

/** One probe at a time: a dead network fails every request in flight. */
let probingOtherAddress = false;

/**
 * Says at once that the request never arrived, and offers the other address
 * only once it proves to answer: a dead server is shut at both.
 */
function announceUnreachable(): void {
  const { error: showError, dismiss, toasts } = useToast();
  showError(UNREACHABLE);

  const [other] = otherSiteAddresses();
  if (!other || probingOtherAddress) return;

  probingOtherAddress = true;
  void addressAnswers(other.host)
    .then((answers) => {
      if (!answers) return;
      const standing = toasts.value.find((t) => t.message === UNREACHABLE);
      if (!standing) return;
      dismiss(standing.id);
      showError(`${UNREACHABLE}, попробуйте [${other.title}]`, 0, () =>
        window.location.assign(
          `${window.location.protocol}//${other.host}${window.location.pathname}${window.location.search}${window.location.hash}`,
        ),
      );
    })
    .finally(() => {
      probingOtherAddress = false;
    });
}

/**
 * Made once: send() hands the caller the same object the interceptor read, and
 * a general refusal is found again by it (generalPlate.ts).
 */
const problems = new WeakMap<object, GeneralError>();

export function problemOf(
  error: object,
  response: AxiosResponse,
): GeneralError {
  let problem = problems.get(error);
  if (!problem) {
    problem = asProblem(response);
    problems.set(error, problem);
  }
  return problem;
}

/**
 * A failed response as a problem document. An unrouted path or a wrong method
 * comes with an empty body, which read as success; the title stays empty for
 * the call site's own sentence.
 */
function asProblem(response: AxiosResponse): GeneralError {
  const body = response.data;

  const problem: GeneralError =
    body && typeof body === "object"
      ? (body as GeneralError)
      : {
          type: "",
          title: "",
          status: response.status,
          traceId: "",
        };

  // Copied in only when the server named a wait, so "did not say" stays apart.
  const seconds = retryAfterSeconds(response);
  return seconds === undefined
    ? problem
    : { ...problem, retryAfterSeconds: seconds };
}

/** How long a request may wait for its answer before axios gives up on it. */
export const REQUEST_TIMEOUT_MS = 30000;

/**
 * When the last 401 of an ended session came back. The 401s of one expiry come
 * within the request timeout of each other and are said once.
 */
let lastExpiryAt: number | null = null;

/** Whether this 401 starts an expiry, rather than repeating the current one. */
function startsExpiry(): boolean {
  const now = Date.now();
  const starts =
    lastExpiryAt === null || now - lastExpiryAt >= REQUEST_TIMEOUT_MS;
  lastExpiryAt = now;
  return starts;
}

/** A 401: the app drops the viewer from the store, once per expiry. */
function announceExpiry(error: AxiosError): void {
  if (
    error.response?.status === 401 &&
    !flag(error.config, "probesSession") &&
    startsExpiry()
  ) {
    const { warning } = useToast();
    warning("Сессия истекла, войдите снова");
    onSessionExpired?.();
  }
}

/**
 * The server's own reason is relayed as it stands. A failure that names none,
 * by an empty body or by the server's mark of a general refusal, gets the
 * general plate with the call's phrase (generalPlate.ts).
 */
function announceFailure(
  error: AxiosError,
  response: AxiosResponse,
  reason: GeneralReason,
): void {
  const failure = problemOf(error, response);
  const said = describeFailure(failure, "");
  if (!said || failure.general) announceGeneral(failure, reason);
  else announceWorded(failure, () => useToast().error(said));
}

function announceRefusal(error: AxiosError): void {
  const { response } = error;
  if (response?.status === 403 && !flag(error.config, "ownsRefusal"))
    announceFailure(error, response, GENERAL_REASONS.forbidden);
}

/**
 * A 429. A form pressed again may own the wait instead (ownsRateLimit), with
 * the same sentence.
 */
function announceRateLimit(error: AxiosError): void {
  const { response } = error;
  if (response?.status === 429 && !flag(error.config, "ownsRateLimit")) {
    const seconds = retryAfterSeconds(response);
    announceWorded(problemOf(error, response), () =>
      useToast().warning(describeRateLimit(seconds)),
    );
  }
}

function announceServerError(error: AxiosError): void {
  const { response } = error;
  if (response && response.status >= 500 && !flag(error.config, "ownsRefusal"))
    announceFailure(error, response, GENERAL_REASONS.server);
}

/**
 * Decides what the reader is told about a failed request, status by status.
 * axios hands over whatever rejected, and only its own failures are read.
 */
export async function onFailedResponse(error: unknown): Promise<never> {
  if (isAxiosError(error)) {
    announceExpiry(error);
    announceRefusal(error);
    announceRateLimit(error);
    announceServerError(error);

    // Nothing came back: a dead connection (ERR_NETWORK) or a timeout
    // (ECONNABORTED), which is how a blocked route fails.
    if (
      !error.response &&
      (error.code === "ERR_NETWORK" || error.code === "ECONNABORTED")
    ) {
      announceUnreachable();
    }
  }

  return Promise.reject(error);
}
