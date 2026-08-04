import type { Upload } from "./models/common/upload";
import type { ListEnvelope } from "./models/common";
import Api from "./client";
import type { AxiosProgressEvent } from "axios";

/**
 * Upload API.
 *
 * Direct upload only: file → multipart/form-data → server side
 * processing → confirmed Upload. Used for avatars, portraits,
 * post attachments. No presigned URLs.
 */
export default new (class uploadApi {
  public directUpload(
    file: File,
    type: string,
    options?: {
      targetId?: string;
      onProgress?: (e: AxiosProgressEvent) => void;
      /**
       * Idempotency-Key for safe retries. If not provided, it is generated
       * automatically via crypto.randomUUID(). The server caches the response for
       * the same key for an hour, so duplicate retries (mobile
       * network, axios-retry interceptor) do not create duplicate Upload records.
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

  /**
   * List uploads, newest first (GET /v1/uploads).
   * Without `username` returns the CURRENT user's own uploads (any
   * authenticated user). Passing `username` narrows to that user's uploads
   * and is Admin-gated server-side (UploadIntention.ListUser).
   */
  public getUploads(params?: {
    /** 1-based page number; the wire takes skip/take like every other list. */
    number?: number;
    /** page size 1-100 */
    size?: number;
    username?: string;
  }) {
    const pageSize = params?.size ?? 20;
    const pageNumber = params?.number ?? 1;
    return Api.get<ListEnvelope<Upload>>("uploads", {
      username: params?.username,
      skip: pageNumber > 1 ? (pageNumber - 1) * pageSize : undefined,
      take: pageSize,
    });
  }

  /**
   * Delete an upload (DELETE /v1/uploads/{id}, soft-delete).
   * Allowed to the upload owner and admins server-side.
   */
  public deleteUpload(id: string) {
    return Api.delete(`uploads/${id}`);
  }
})();
