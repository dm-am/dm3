import axios, { AxiosError } from "axios";

import type {
  AxiosRequestConfig,
  AxiosInstance,
  AxiosResponse,
  AxiosProgressEvent,
} from "axios";
import type { HubConnection } from "@microsoft/signalr";
import type { ApiResult, GeneralError } from "./models/common";
import {
  RENDER_AUDIENCE,
  X_DM_AUDIENCE,
  type RenderAudience,
} from "./audience";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "@/shared/lib/errors";

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
   * belongs under the field with the rest of the answer, and the generic
   * "Недостаточно прав для этого действия" replaced a reason with a sentence
   * that names nothing.
   */
  ownsRefusal?: boolean;
};

/** An axios config with this client's own options riding along on it. */
type TaggedRequest = AxiosRequestConfig & RequestOptions;

/** Whether the request that failed said it would show the refusal itself. */
function ownsRefusal(request?: AxiosRequestConfig): boolean {
  return Boolean((request as TaggedRequest | undefined)?.ownsRefusal);
}

const defaultHeaders: { [key: string]: string } = {
  "Cache-Control": "no-cache",
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
function asProblem(response: AxiosResponse): GeneralError {
  const body = response.data;

  if (body && typeof body === "object") {
    return body as GeneralError;
  }

  return {
    type: "",
    title: "",
    status: response.status,
    traceId: "",
  };
}

const configuration: AxiosRequestConfig = {
  baseURL: `${apiHost}/v1`,
  headers: defaultHeaders,
  responseType: "json",
  timeout: 30000,
  withCredentials: true, // Required for HttpOnly cookie authentication
  paramsSerializer: {
    // ASP.NET Core expects: key=val1&key=val2 (no brackets/indices)
    indexes: null,
  },
};

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
        if (error.response?.status === 401) {
          const { warning } = useToast();
          warning("Сессия истекла. Пожалуйста, войдите снова.");
          onSessionExpired?.();
        }

        // Handle 403 Forbidden — say which refusal it was.
        // The API names the reason: 68 throw sites answer 403 with a sentence
        // of their own ("Вы в черном списке этого блога", "Аккаунт
        // заблокирован"), and an authorization refusal is answered with one
        // constant title, so the title is always safe to relay as it stands.
        // Showing the general sentence over all of them left a blacklisted
        // reader in front of a working comment box with nothing to learn from:
        // the text comes back, the toast says the rights are missing, and the
        // reason it will never be accepted was on the wire and thrown away.
        // The fallback is for a 403 the error middleware never saw — the
        // framework answers those with no body at all.
        if (error.response?.status === 403 && !ownsRefusal(error.config)) {
          const { error: showError } = useToast();
          const refusal = asProblem(error.response);
          showError(
            describeFailure(refusal, "Недостаточно прав для этого действия"),
          );
        }

        // Handle 429 Too Many Requests
        if (error.response?.status === 429) {
          const { warning } = useToast();
          const retryAfter = error.response.headers?.["retry-after"];
          warning(
            retryAfter
              ? `Слишком много запросов. Повторите через ${retryAfter} сек.`
              : "Слишком много запросов. Повторите позже.",
          );
        }

        // Handle 500+ Server Errors
        if (error.response?.status >= 500) {
          const { error: showError } = useToast();
          showError("Ошибка сервера. Попробуйте позже.");
        }

        // Handle network errors (no response)
        if (!error.response && error.code === "ERR_NETWORK") {
          const { error: showError } = useToast();
          showError("Нет соединения с сервером");
        }

        return Promise.reject(error);
      },
    );
  }

  public get<T>(
    url: string,
    params?: QueryParams,
    audience: RenderAudience = RENDER_AUDIENCE.Display,
    options?: { skipAuth?: boolean; headers?: Record<string, string> },
  ): Promise<ApiResult<T>> {
    // A token-gated endpoint reads its credential from a header. A URL is written
    // verbatim into the proxy access log and into the trace; a header is written
    // to neither.
    const headers: Record<string, string> = {
      [X_DM_AUDIENCE]: audience,
      ...options?.headers,
    };

    // For public endpoints, explicitly remove credentials
    // This prevents activity tracking from background polling
    if (options?.skipAuth) {
      return this.send(() =>
        this.axios.get(url, {
          params,
          headers,
          withCredentials: false,
        }),
      );
    }

    return this.send(() => this.axios.get(url, { params, headers }));
  }

  public post<T>(
    url: string,
    body?: RequestBody,
    options?: RequestOptions & { headers?: Record<string, string> },
  ): Promise<ApiResult<T>> {
    const request: TaggedRequest = {
      ownsRefusal: options?.ownsRefusal,
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

  public put<T>(url: string, body?: RequestBody): Promise<ApiResult<T>> {
    return this.send(() => this.axios.put(url, body));
  }

  public patch<T>(url: string, body?: RequestBody): Promise<ApiResult<T>> {
    return this.send(() => this.axios.patch(url, body));
  }

  public delete(url: string): Promise<ApiResult<void>> {
    return this.send(() => this.axios.delete(url));
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
        return { data: null, error: asProblem(err.response) };
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
      .withAutomaticReconnect()
      .withUrl(`${apiHost}/${path}`, {
        withCredentials: true,
      })
      .build();
  }
}

export default new Api();
