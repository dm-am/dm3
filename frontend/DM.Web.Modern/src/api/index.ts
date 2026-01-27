import axios, { AxiosError } from "axios";

import type {
  AxiosRequestConfig,
  AxiosInstance,
  AxiosResponse,
  AxiosProgressEvent,
} from "axios";
import { HubConnection, HubConnectionBuilder } from "@microsoft/signalr";
import type { ApiResult } from "@/api/models/common";
import { BbRenderMode } from "./bbRenderMode";

type QueryParams = Record<string, string | number | boolean | undefined>;
type RequestBody = object | FormData;

const accessTokenKey = "dm-access-token";
const refreshTokenKey = "dm-refresh-token";
const renderKey = "x-dm-bb-render-mode";

const defaultHeaders: { [key: string]: string } = {
  "Cache-Control": "no-cache",
  "Content-Type": "application/json",
  "X-Requested-With": "XMLHttpRequest", // CSRF protection - identifies AJAX requests
  [renderKey]: "html",
};

const apiHost = import.meta.env.VITE_API_HOST ?? "http://localhost:5051"; // Config - use ?? to allow empty string

const configuration: AxiosRequestConfig = {
  baseURL: `${apiHost}/v1`,
  headers: defaultHeaders,
  responseType: "json",
};

interface TokenResponse {
  access_token: string;
  refresh_token: string;
  token_type: string;
  expires_in: number;
}

class Api {
  private axios: AxiosInstance;
  private isRefreshing = false;
  private refreshQueue: Array<{
    resolve: (token: string) => void;
    reject: (error: unknown) => void;
  }> = [];

  constructor() {
    this.axios = axios.create(configuration);

    // Initialize with access token if present
    const accessToken = localStorage.getItem(accessTokenKey);
    if (accessToken) {
      this.axios.defaults.headers.common["Authorization"] = `Bearer ${accessToken}`;
    }

    // Add response interceptor for token refresh
    this.axios.interceptors.response.use(
      (response) => response,
      async (error) => {
        const originalRequest = error.config;

        // Handle 401 errors with token refresh
        if (error.response?.status === 401 && !originalRequest._retry) {
          originalRequest._retry = true;

          const refreshToken = localStorage.getItem(refreshTokenKey);
          if (refreshToken) {
            try {
              // If already refreshing, queue this request
              if (this.isRefreshing) {
                return new Promise((resolve, reject) => {
                  this.refreshQueue.push({ resolve, reject });
                })
                  .then((token) => {
                    originalRequest.headers["Authorization"] = `Bearer ${token}`;
                    return this.axios.request(originalRequest);
                  })
                  .catch((err) => Promise.reject(err));
              }

              this.isRefreshing = true;

              const tokens = await this.refreshAccessToken(refreshToken);
              this.updateTokens(tokens);

              // Process queued requests
              this.refreshQueue.forEach((callback) => {
                callback.resolve(tokens.access_token);
              });
              this.refreshQueue = [];

              // Retry original request with new token
              originalRequest.headers["Authorization"] = `Bearer ${tokens.access_token}`;
              return this.axios.request(originalRequest);
            } catch (refreshError) {
              // Refresh failed, clear tokens and queue
              this.refreshQueue.forEach((callback) => {
                callback.reject(refreshError);
              });
              this.refreshQueue = [];
              this.clearAuthenticationInfo();

              // Don't redirect - let the app handle unauthenticated state
              return Promise.reject(refreshError);
            } finally {
              this.isRefreshing = false;
            }
          } else {
            // No refresh token, just clear auth (don't redirect)
            this.clearAuthenticationInfo();
          }
        }

        return Promise.reject(error);
      }
    );
  }

  public isAuthenticated(): boolean {
    return "Authorization" in this.axios.defaults.headers.common;
  }

  /**
   * Refresh access token using refresh token
   */
  public async refreshAccessToken(refreshToken: string): Promise<TokenResponse> {
    const response = await fetch(`${apiHost}/connect/token`, {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: new URLSearchParams({
        grant_type: "refresh_token",
        refresh_token: refreshToken,
        client_id: "dm3-web",
      }),
    });

    if (!response.ok) {
      throw new Error("Token refresh failed");
    }

    return response.json();
  }

  /**
   * Fetch OAuth2 access token using password grant
   */
  public async fetchOAuthToken(
    username: string,
    password: string
  ): Promise<TokenResponse> {
    const response = await fetch(`${apiHost}/connect/token`, {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: new URLSearchParams({
        grant_type: "password",
        username,
        password,
        client_id: "dm3-web",
        scope: "openid profile offline_access",
      }),
    });

    if (!response.ok) {
      const errorData = await response.json();
      throw new Error(errorData.error_description || "Authentication failed");
    }

    return response.json();
  }

  /**
   * Update stored tokens
   */
  public updateTokens(tokens: TokenResponse): void {
    localStorage.setItem(accessTokenKey, tokens.access_token);
    localStorage.setItem(refreshTokenKey, tokens.refresh_token);
    this.axios.defaults.headers.common["Authorization"] = `Bearer ${tokens.access_token}`;
  }

  public get<T>(
    url: string,
    params?: QueryParams,
    bbRenderMode: BbRenderMode = BbRenderMode.Html,
  ): Promise<ApiResult<T>> {
    return this.send(() =>
      this.axios.get(url, { params, headers: { [renderKey]: bbRenderMode } }),
    );
  }

  public post<T>(url: string, body?: RequestBody): Promise<ApiResult<T>> {
    return this.send(() => this.axios.post(url, body));
  }

  /*
   Поскольку мы отслеживаем прогресс только отправки файла на сервер с клиента,
   обработчик прогресса "зависает" на 99% до момента получения окончательного ответа от сервера.
  */
  public postFile<T>(
    url: string,
    formData: FormData,
    progressCallback?: (event: AxiosProgressEvent) => void,
  ): Promise<ApiResult<T>> {
    const result = this.send<T>(() =>
      this.axios.post(url, formData, {
        headers: {
          "Content-Type": "multipart/form-data",
        },
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
        return { data: null, error: err.response.data };
      }
      return {
        data: null,
        error: {
          type: "Unknown",
          title: "Unknown error",
          status: 0,
          traceId: "",
        },
      };
    }
  }

  public logout() {
    this.clearAuthenticationInfo();
  }

  public establishHubConnection(path: string): HubConnection {
    const token = localStorage.getItem(accessTokenKey);

    return new HubConnectionBuilder()
      .withAutomaticReconnect()
      .withUrl(`${apiHost}/${path}`, {
        accessTokenFactory() {
          return token ?? "";
        },
      })
      .build();
  }

  private clearAuthenticationInfo(): void {
    delete this.axios.defaults.headers.common["Authorization"];
    localStorage.removeItem(accessTokenKey);
    localStorage.removeItem(refreshTokenKey);
  }
}

export default new Api();
