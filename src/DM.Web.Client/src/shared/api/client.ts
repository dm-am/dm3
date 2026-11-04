import axios, { AxiosError } from "axios";

import type {
  AxiosRequestConfig,
  AxiosInstance,
  AxiosResponse,
  AxiosProgressEvent,
} from "axios";
import type { HubConnection, IRetryPolicy } from "@microsoft/signalr";
import type { ApiResult } from "./models/common";
import {
  RENDER_AUDIENCE,
  X_DM_AUDIENCE,
  type RenderAudience,
} from "./audience";
import {
  onFailedResponse,
  problemOf,
  REQUEST_TIMEOUT_MS,
  tagged,
  type RequestOptions,
} from "./interceptor";
import { requestNotSent } from "@/shared/lib/errors/requestNotSent";

type QueryParams = Record<
  string,
  string | number | boolean | string[] | number[] | undefined
>;
type RequestBody = object | FormData;

// No Cache-Control: what may be cached is the server's decision per endpoint,
// and no-cache on the request would forbid every cache hit.
const defaultHeaders: { [key: string]: string } = {
  "Content-Type": "application/json",
  [X_DM_AUDIENCE]: RENDER_AUDIENCE.Display,
};

const apiHost = import.meta.env.VITE_API_HOST ?? "http://localhost:5000"; // Config - use ?? to allow empty string

/**
 * Absolute address of a path the API answers on. The server sends paths, not
 * hosts: behind the proxy only the client knows the origin.
 */
export function apiUrl(path: string): string {
  return `${apiHost}${path}`;
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

// The default gives up after four attempts, longer than a deploy takes, and
// the badges have no polling to fall back on: retry forever, capped. The host
// resubscribes on the same ladder (ReconnectLadderShould).
export const hubRetryPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: (context) =>
    Math.min(30000, 1000 * 2 ** context.previousRetryCount),
};

class Api {
  private axios: AxiosInstance;

  constructor() {
    this.axios = axios.create(configuration);

    this.axios.interceptors.response.use(
      (response) => response,
      onFailedResponse,
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
    // A token travels in a header: a URL is written into the access log.
    const headers: Record<string, string> = {
      [X_DM_AUDIENCE]: audience,
      ...options?.headers,
    };

    const request = { ...tagged(options), params, headers };

    // Without credentials, background polling of a public endpoint is not activity.
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
    const request = { ...tagged(options), headers: options?.headers };
    return this.send(() => this.axios.post(url, body, request));
  }

  // Only the upload is tracked, so the progress stops at 99% until the server answers.
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
        data,
        error: null,
      };
    } catch (err: unknown) {
      if (err instanceof AxiosError && err.response) {
        return {
          data: null,
          error: problemOf(err, err.response),
        };
      }
      // No response at all.
      return { data: null, error: requestNotSent };
    }
  }

  // Imported on demand to keep 56 kB out of the entry bundle of every visit.
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
