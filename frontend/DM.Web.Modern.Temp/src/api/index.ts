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

const tokenKey = "x-dm-auth-token";
const renderKey = "x-dm-bb-render-mode";

const defaultHeaders: { [key: string]: string } = {
  "Cache-Control": "no-cache",
  "Content-Type": "application/json",
  [renderKey]: "html",
};

const apiHost = import.meta.env.VITE_API_HOST || "http://localhost:5051"; // Config

const configuration: AxiosRequestConfig = {
  baseURL: `${apiHost}/v1`,
  headers: defaultHeaders,
  responseType: "json",
};

class Api {
  private axios: AxiosInstance;

  constructor() {
    this.axios = axios.create(configuration);

    const token = localStorage.getItem(tokenKey);
    if (token) {
      this.updateAuthenticationInfo(token);
    }
  }

  public isAuthenticated(): boolean {
    return tokenKey in this.axios.defaults.headers.common;
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
      const { data, headers } = await sender();
      if (tokenKey in headers) {
        this.updateAuthenticationInfo(headers[tokenKey]);
      }
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
        error: { type: "Unknown", title: "Unknown error", status: 0, traceId: "" },
      };
    }
  }

  public logout() {
    this.clearAuthenticationInfo();
  }

  public establishHubConnection(path: string): HubConnection {
    const token = this.axios.defaults.headers.common[tokenKey] as string;
    return new HubConnectionBuilder()
      .withAutomaticReconnect()
      .withUrl(`${apiHost}/${path}`, {
        accessTokenFactory() {
          return token!;
        },
      })
      .build();
  }

  private updateAuthenticationInfo(token: string): void {
    this.axios.defaults.headers.common[tokenKey] = token;
    localStorage.setItem(tokenKey, token);
  }

  private clearAuthenticationInfo(): void {
    delete this.axios.defaults.headers.common[tokenKey];
    localStorage.removeItem(tokenKey);
  }
}

export default new Api();
