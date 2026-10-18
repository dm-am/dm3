import axios, { AxiosError } from "axios";

import type {
  AxiosRequestConfig,
  AxiosInstance,
  AxiosResponse,
  AxiosProgressEvent,
} from "axios";
import type { HubConnection, IRetryPolicy } from "@microsoft/signalr";
import type { ApiResult, GeneralError } from "./models/common";
import {
  RENDER_AUDIENCE,
  X_DM_AUDIENCE,
  type RenderAudience,
} from "./audience";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  ACCESS_DENIED,
  announceGeneral,
  announceWorded,
  describeFailure,
  describeRateLimit,
  GENERAL_REASONS,
  SERVER_ERROR,
} from "@/shared/lib/errors";
import { otherSiteAddresses } from "@/shared/config/site";
import { addressAnswers } from "@/shared/lib/composables/useAddressPing";

type QueryParams = Record<
  string,
  string | number | boolean | string[] | number[] | undefined
>;
type RequestBody = object | FormData;

/**
 * Options this client understands beyond the ones axios has. They ride on the
 * request config, which is where the response interceptor finds them again on
 * the request that failed.
 */
type RequestOptions = {
  /**
   * The caller shows the refusal itself, so the interceptor says nothing about
   * a 403 on this request.
   *
   * The interceptor speaks for the statuses it knows more about than the call
   * site does, and a 403 is one of them nearly everywhere: the caller knows
   * which button was pressed, not why the server said no. Signing in is the
   * exception. There the refusal is the whole answer to the submit — the
   * account is banned, removed, or locked out after too many attempts — it
   * belongs under the field with the rest of the answer, and the general
   * plate ("...: недостаточно прав") replaced a reason with a sentence that
   * names nothing.
   */
  ownsRefusal?: boolean;

  /**
   * The caller shows the rate limit itself, so the interceptor says nothing
   * about a 429 on this request.
   *
   * A flag of its own rather than a wider reading of `ownsRefusal`: that one is
   * set on requests that have no opinion at all about a rate limit, and
   * stretching it over 429 would take their toast away without anyone asking
   * for it. What earns this flag is a form the reader presses again — the way
   * in, above all — where the answer belongs in the form's own strip, with the
   * wait running down on the button, and a toast beside that is the same
   * refusal said twice.
   */
  ownsRateLimit?: boolean;

  /**
   * The request asks whether there is a session at all, so a 401 is an answer
   * ("nobody") and not a session that ran out: the interceptor neither says
   * "Сессия истекла" nor hands the event to the app.
   *
   * The one caller is the start of the page with no viewer stored in the
   * browser: the cookie may still hold a session the page knows nothing about,
   * and every guest who asks gets the 401 that says so. Read as an expiry, that
   * answer put a warning in front of every guest on every visit.
   */
  probesSession?: boolean;
};

/** An axios config with this client's own options riding along on it. */
type TaggedRequest = AxiosRequestConfig & RequestOptions;

/**
 * The flags a call site sets, in the shape axios carries to the interceptor.
 *
 * Every method takes them, not only post: a page that shows a failed load in
 * its own strip is exactly the case the flag exists for, and until now only a
 * press could claim it. The result was the same sentence in two places at
 * once - once in the strip, once in a plate over it.
 */
const tagged = (options?: RequestOptions): TaggedRequest => ({
  ownsRefusal: options?.ownsRefusal,
  ownsRateLimit: options?.ownsRateLimit,
  probesSession: options?.probesSession,
});

/** Whether the request that failed said it would show the refusal itself. */
function ownsRefusal(request?: AxiosRequestConfig): boolean {
  return Boolean((request as TaggedRequest | undefined)?.ownsRefusal);
}

/** Whether the request that failed said it would show the rate limit itself. */
function ownsRateLimit(request?: AxiosRequestConfig): boolean {
  return Boolean((request as TaggedRequest | undefined)?.ownsRateLimit);
}

/** Whether the request that failed was asking if there is a session at all. */
function probesSession(request?: AxiosRequestConfig): boolean {
  return Boolean((request as TaggedRequest | undefined)?.probesSession);
}

/**
 * Whole seconds off the Retry-After header, or undefined when the server named
 * no usable number.
 *
 * The API writes the value as a count of seconds (RateLimitingExtensions,
 * OnRejected) and the edge exposes the header by name, so the HTTP-date form of
 * the header never arrives here and nothing else has to be parsed. Anything
 * else — a missing header, a header the CORS policy hid, a zero — reads as
 * "the server did not say", and the sentence without a number is what stands.
 */
function retryAfterSeconds(response: AxiosResponse): number | undefined {
  const header = response.headers?.["retry-after"];
  if (header === undefined || header === null || header === "") {
    return undefined;
  }

  const seconds = Number(header);
  return Number.isFinite(seconds) && seconds > 0
    ? Math.ceil(seconds)
    : undefined;
}

/**
 * Headers on every request.
 *
 * No Cache-Control here. It used to say `no-cache` on all of them, which
 * answered no question this client has — the session is a cookie, not a cached
 * response — and cost both caches every hit they could have had: `no-cache` on
 * the request forbids ResponseCachingMiddleware from reading its store, and
 * makes the browser revalidate before reusing anything of its own. With no
 * ETag and no Last-Modified in the API, that revalidation is a full request.
 *
 * What may be cached is the origin's decision and it states it per endpoint:
 * five catalogues answer `public, max-age=300`, the two lists carrying
 * per-caller unread counters answer `no-store, no-cache`, and the rest carry
 * no policy at all — which, without a validator to revalidate against, is not
 * reusable either.
 */
const defaultHeaders: { [key: string]: string } = {
  "Content-Type": "application/json",
  // Marks the request as XHR. Not a CSRF control: the server never reads this header.
  // CSRF is covered by the Origin/Referer check in the API and SameSite=Lax on the
  // session cookie.
  "X-Requested-With": "XMLHttpRequest",
  [X_DM_AUDIENCE]: RENDER_AUDIENCE.Display,
};

/**
 * Where to send the user when the server says their session is gone.
 *
 * The HTTP client is a `shared` module and must not know the router, which
 * lives in `app` — the layer above. It used to reach for it with a dynamic
 * import, which hid the inverted dependency rather than removing it. The app
 * installs its own handler at startup — it drops the viewer from the auth store
 * and navigates; until it does, an expired session only shows the toast.
 */
let onSessionExpired: (() => void) | null = null;

/** Installed once by the app layer, which owns navigation. */
export function setSessionExpiredHandler(handler: () => void): void {
  onSessionExpired = handler;
}

const apiHost = import.meta.env.VITE_API_HOST ?? "http://localhost:5000"; // Config - use ?? to allow empty string

/**
 * Absolute address of a path the API answers on.
 *
 * For the few payload fields that are addresses rather than data — an upload's
 * content endpoint, a post attachment — the server sends the path it routes and
 * leaves the origin alone: it is reached at one origin per deployment, behind a
 * proxy that would make any host name the server composed for itself wrong. The
 * client is where the origin is known, and this is the one place that knows it.
 *
 * Behind the edge the two are the same origin and the host part is empty, so the
 * result is the path unchanged.
 */
export function apiUrl(path: string): string {
  return `${apiHost}${path}`;
}

/**
 * A failed response as a problem document, whatever the server actually sent.
 *
 * The middleware answers every error it handles with one, but not every error
 * reaches the middleware: an unrouted path and a wrong method are answered by
 * the framework with a zero-length body. Axios then sets `response.data` to the
 * empty string, and returning that as `error` handed every call site a falsy
 * value — so `if (error)` read a 404 as success with no data, and a store that
 * checks it went down its "there is nothing more" branch instead of reporting
 * the failure.
 *
 * The title is left empty for a synthesised one on purpose: call sites read
 * `error.title || "не удалось ..."`, and the page knows what it was doing
 * better than a generic sentence here would.
 */
/**
 * What a reader is told when a request came back with nothing at all.
 *
 * It names the site: "Этот адрес не отвечает" said which of two things was
 * silent only to someone who already knew the site has two addresses.
 */
const UNREACHABLE = "Сайт не отвечает по этому адресу";

/** One probe at a time: a dead network fails every request in flight. */
let probingOtherAddress = false;

/**
 * Says that the request never arrived, and offers the other address only when
 * that one is proven to answer.
 *
 * Nothing came back, and from here two different worlds look identical: the
 * server is down, or the road to this address is closed for this reader. Only
 * the second one the other address can fix, and offering it in the first case
 * sends somebody to a door that is just as shut. So the sentence goes up at
 * once - waiting on a probe would leave a failed press silent for seconds -
 * and the offer is added to it a moment later, if and only if the other
 * address answered. A reader who has already dismissed the first sentence is
 * left alone.
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
      showError(`${UNREACHABLE}, попробуйте [${other.name}]`, 0, () =>
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
 * The problem document of a failed request, made once.
 *
 * The interceptor reads it to decide what to say, and send() hands it to the
 * caller. They must be the same object: a general refusal waits for the caller
 * to name it (generalPlate.ts), and it is found again by this object.
 */
const problems = new WeakMap<object, GeneralError>();

function problemOf(error: { response: AxiosResponse }): GeneralError {
  let problem = problems.get(error);
  if (!problem) {
    problem = asProblem(error.response);
    problems.set(error, problem);
  }
  return problem;
}

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

  // The wait a rate limit carries is in a header, and the caller is handed the
  // document alone. Copied in only when the server named one, so a caller can
  // tell "wait this long" from "the server did not say" by the field being
  // there at all.
  const seconds = retryAfterSeconds(response);
  return seconds === undefined
    ? problem
    : { ...problem, retryAfterSeconds: seconds };
}

/** How long a request may wait for its answer before axios gives up on it. */
const REQUEST_TIMEOUT_MS = 30000;

/**
 * When the last 401 of an ended session came back, or null before any did.
 *
 * One expiry is one event, however many requests it answers. A page asks for
 * ten things at once and the session dies under all of them together; each
 * 401 used to end the session again and raise "Сессия истекла" again. The
 * plate keeps one copy of its text while it stands, but a 401 that came back
 * after it faded or was closed raised it anew, and the app's handler ran once
 * per request.
 *
 * So the 401s of one expiry are one run: each comes within the request timeout
 * of the one before. A request in flight when the session died is answered or
 * given up within that time, so none of them says it a second time, and a dead
 * cookie that keeps being refused keeps the run going instead of raising the
 * plate every time it faded. A 401 after a quiet longer than that is a session
 * that ended anew, and it is said again.
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

const configuration: AxiosRequestConfig = {
  baseURL: `${apiHost}/v1`,
  headers: defaultHeaders,
  responseType: "json",
  timeout: REQUEST_TIMEOUT_MS,
  withCredentials: true, // Required for HttpOnly cookie authentication
  paramsSerializer: {
    // ASP.NET Core expects: key=val1&key=val2 (no brackets/indices)
    indexes: null,
  },
};

/**
 * Reconnection policy of the realtime hub.
 *
 * The default is four attempts — 0, 2, 10 and 30 seconds — and then the
 * connection is closed for good, with nothing on the client side trying again.
 * An API restart longer than that, which an ordinary deploy with a warm-up is,
 * left every open tab without realtime until the reader happened to reload the
 * page: the global chat survived on its own polling fallback, while the
 * messenger badge and the notification bell, which have none, froze on the
 * number the page had loaded with. So the retries never stop and the delay is
 * capped instead.
 */
export const hubRetryPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (context) =>
    Math.min(30000, 1000 * 2 ** context.previousRetryCount),
};

/**
 * How often a live list re-reads its tail while the socket is not delivering
 * pushes and the tab is visible. One number for every such list (the global
 * chat today), declared beside the reconnect policy because both answer the
 * same question: how long a reader may look at a stale screen. Thirty seconds
 * is the global chat's own figure, kept as it was.
 */
export const LIVE_TAIL_POLL_INTERVAL_MS = 30000;

class Api {
  private axios: AxiosInstance;

  constructor() {
    this.axios = axios.create(configuration);

    // Add response interceptor for error handling
    this.axios.interceptors.response.use(
      (response) => response,
      async (error) => {
        // Handle 401 Unauthorized — session expired or invalid.
        // Who the viewer is belongs to the auth store, which owns the persisted
        // copy. Clearing that copy from here left the store still holding the
        // user, so the header, the sidebar blocks and every action button kept
        // rendering as signed in while the route guard bounced the same viewer.
        // Said and handled once per expiry (startsExpiry above).
        if (
          error.response?.status === 401 &&
          !probesSession(error.config) &&
          startsExpiry()
        ) {
          const { warning } = useToast();
          warning("Сессия истекла, войдите снова");
          onSessionExpired?.();
        }

        // Handle 403 Forbidden — say which refusal it was.
        // The API names the reason: 68 throw sites answer 403 with a sentence
        // of their own ("Вы в черном списке этого блога", "Аккаунт
        // заблокирован"), and those are relayed as they stand. Showing the
        // general sentence over all of them left a blacklisted reader in front
        // of a working comment box with nothing to learn from: the text comes
        // back, the toast says the rights are missing, and the reason it will
        // never be accepted was on the wire and thrown away.
        //
        // What is left names no reason: the one constant title of an
        // authorization refusal, and a 403 the error middleware never saw,
        // which the framework answers with no body at all. That plate says
        // only "недостаточно прав", so the call's own phrase goes at its head.
        //
        // Either plate is held for a moment (generalPlate.ts): a page that
        // writes the refusal at the place of the press takes it down, and one
        // refusal is not said a second time in the corner.
        if (error.response?.status === 403 && !ownsRefusal(error.config)) {
          const refusal = problemOf(error);
          const said = describeFailure(refusal, "");
          if (!said || said === ACCESS_DENIED)
            announceGeneral(refusal, GENERAL_REASONS.forbidden);
          else announceWorded(refusal, () => useToast().error(said));
        }

        // Handle 429 Too Many Requests.
        // A toast is the wrong carrier wherever the reader is about to press
        // the same button again: it fades after eight seconds, the form under
        // it does not change, and the second press earns the same refusal. So a
        // request may take the limit over — the guest forms do — and then the
        // sentence stands in the form with the seconds running down, and this
        // says nothing. The wording is shared with them, so there is one
        // sentence and not two. A page that writes the wait at the place of
        // the press takes the held plate down the same way.
        if (error.response?.status === 429 && !ownsRateLimit(error.config)) {
          const seconds = retryAfterSeconds(error.response);
          announceWorded(problemOf(error), () =>
            useToast().warning(describeRateLimit(seconds)),
          );
        }

        // Handle 500+ Server Errors.
        // The API titles every one of them SERVER_ERROR, and an edge that
        // refuses a request answers with no document at all. Neither says
        // which press it answers, so the plate is the general one with the
        // call's phrase at its head: "Не удалось сохранить пост: ошибка
        // сервера". A 5xx that did name something else is relayed as it
        // stands, the way the 403 above relays its own.
        if (error.response?.status >= 500 && !ownsRefusal(error.config)) {
          const failure = problemOf(error);
          const said = describeFailure(failure, "");
          if (!said || said === SERVER_ERROR)
            announceGeneral(failure, GENERAL_REASONS.server);
          else announceWorded(failure, () => useToast().error(said));
        }

        // Nothing came back at all. Two codes mean that: axios answers a dead
        // connection with ERR_NETWORK and a request that ran out of time with
        // ECONNABORTED. The second was falling through silently, and a route
        // that is blocked times out rather than refusing, so the visitor who
        // needed this sentence most was the one who never saw it.
        //
        // What is said about it, and when the other address is worth naming,
        // is decided in announceUnreachable above.
        if (
          !error.response &&
          (error.code === "ERR_NETWORK" || error.code === "ECONNABORTED")
        ) {
          announceUnreachable();
        }

        return Promise.reject(error);
      },
    );
  }

  public get<T>(
    url: string,
    params?: QueryParams,
    audience: RenderAudience = RENDER_AUDIENCE.Display,
    options?: RequestOptions & {
      skipAuth?: boolean;
      headers?: Record<string, string>;
    },
  ): Promise<ApiResult<T>> {
    // A token-gated endpoint reads its credential from a header. A URL is written
    // verbatim into the proxy access log and into the trace; a header is written
    // to neither.
    const headers: Record<string, string> = {
      [X_DM_AUDIENCE]: audience,
      ...options?.headers,
    };

    // Carried on the request config, which is where the response interceptor
    // finds it again: a page that draws the refusal in its own strip says so
    // here, and the interceptor keeps quiet instead of putting the same
    // sentence in a second place.
    const request: TaggedRequest = {
      params,
      headers,
      ownsRefusal: options?.ownsRefusal,
      ownsRateLimit: options?.ownsRateLimit,
      probesSession: options?.probesSession,
    };

    // For public endpoints, explicitly remove credentials
    // This prevents activity tracking from background polling
    if (options?.skipAuth) {
      return this.send(() =>
        this.axios.get(url, { ...request, withCredentials: false }),
      );
    }

    return this.send(() => this.axios.get(url, request));
  }

  public post<T>(
    url: string,
    body?: RequestBody,
    options?: RequestOptions & { headers?: Record<string, string> },
  ): Promise<ApiResult<T>> {
    const request: TaggedRequest = {
      ownsRefusal: options?.ownsRefusal,
      ownsRateLimit: options?.ownsRateLimit,
      headers: options?.headers,
    };
    return this.send(() => this.axios.post(url, body, request));
  }

  /*
   Since we only track the progress of sending the file from the client to the server,
   the progress handler "hangs" at 99% until the final server response arrives.
  */
  public postFile<T>(
    url: string,
    formData: FormData,
    progressCallback?: (event: AxiosProgressEvent) => void,
    idempotencyKey?: string,
  ): Promise<ApiResult<T>> {
    const headers: Record<string, string> = {
      "Content-Type": "multipart/form-data",
    };
    if (idempotencyKey) {
      headers["Idempotency-Key"] = idempotencyKey;
    }
    const result = this.send<T>(() =>
      this.axios.post(url, formData, {
        headers,
        onUploadProgress: progressCallback
          ? (event: AxiosProgressEvent) =>
              progressCallback(
                event.loaded === event.total
                  ? ({ loaded: 99, total: 100 } as AxiosProgressEvent)
                  : event,
              )
          : undefined,
      }),
    );
    progressCallback?.({ loaded: 1, total: 1 } as AxiosProgressEvent);
    return result;
  }

  public put<T>(
    url: string,
    body?: RequestBody,
    options?: RequestOptions,
  ): Promise<ApiResult<T>> {
    return this.send(() => this.axios.put(url, body, tagged(options)));
  }

  public patch<T>(
    url: string,
    body?: RequestBody,
    options?: RequestOptions,
  ): Promise<ApiResult<T>> {
    return this.send(() => this.axios.patch(url, body, tagged(options)));
  }

  public delete(
    url: string,
    body?: RequestBody,
    options?: RequestOptions,
  ): Promise<ApiResult<void>> {
    return this.send(() =>
      this.axios.delete(url, {
        ...tagged(options),
        ...(body ? { data: body } : {}),
      }),
    );
  }

  private async send<T>(
    sender: () => Promise<AxiosResponse<T>>,
  ): Promise<ApiResult<T>> {
    try {
      const { data } = await sender();
      return {
        data: data as T,
        error: null,
      };
    } catch (err: unknown) {
      if (err instanceof AxiosError && err.response) {
        return {
          data: null,
          error: problemOf(err as { response: AxiosResponse }),
        };
      }
      // No response at all: the request never reached the API, or the browser
      // cut it. The title is left empty on purpose — call sites read
      // `error.title || "не удалось ..."`, and any English placeholder here is
      // truthy, so it won that fallback and shipped "Unknown error" to the
      // reader instead of the message the page wrote for exactly this case.
      return {
        data: null,
        error: {
          type: "Unknown",
          title: "",
          status: 0,
          traceId: "",
        },
      };
    }
  }

  /**
   * Establish SignalR hub connection.
   * Cookies are sent automatically with withCredentials.
   *
   * The client is imported here rather than at the top of the file so it stays
   * out of the entry bundle: a static import put 56 kB raw on the critical path
   * of every visit, including the guest ones that never open a socket. The
   * connection is negotiated asynchronously in any case, so the extra request
   * costs nothing that was not already being awaited.
   */
  public async establishHubConnection(path: string): Promise<HubConnection> {
    const { HubConnectionBuilder } = await import("@microsoft/signalr");
    return new HubConnectionBuilder()
      .withAutomaticReconnect(hubRetryPolicy)
      .withUrl(`${apiHost}/${path}`, {
        withCredentials: true,
      })
      .build();
  }
}

export default new Api();
