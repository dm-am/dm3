import type { Upload } from "./models/common/upload";
import Api from "./client";
import type { AxiosProgressEvent } from "axios";

/**
 * Upload API.
 *
 * Direct upload only: file → multipart/form-data → server side
 * processing → confirmed Upload. Применяется для аватаров, портретов,
 * вложений постов. Никаких presigned URLs.
 */
export default new (class UploadApi {
  public directUpload(
    file: File,
    type: string,
    options?: {
      targetId?: string;
      onProgress?: (e: AxiosProgressEvent) => void;
      /**
       * Idempotency-Key для безопасного retry. Если не задан — генерируется
       * crypto.randomUUID() автоматически. Сервер кеширует response для
       * того же ключа в течение часа, поэтому дублирующие retry'и (mobile
       * сеть, axios-retry interceptor) не создают дубли Upload-записей.
       */
      idempotencyKey?: string;
    },
  ) {
    const formData = new FormData();
    formData.append("file", file);
    const query = new URLSearchParams({ type });
    if (options?.targetId) {
      query.set("targetId", options.targetId);
    }
    const key =
      options?.idempotencyKey ??
      (typeof crypto !== "undefined" && crypto.randomUUID
        ? crypto.randomUUID()
        : `${Date.now()}-${Math.random().toString(36).slice(2)}`);
    return Api.postFile<Upload>(
      `uploads?${query.toString()}`,
      formData,
      options?.onProgress,
      key,
    );
  }

  public getUpload(uploadId: string) {
    return Api.get<Upload>(`uploads/${uploadId}`);
  }

  public deleteUpload(uploadId: string) {
    return Api.delete(`uploads/${uploadId}`);
  }
})();
