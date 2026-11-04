import type { Upload, UploadAudience } from "./models/common/upload";
import type { Envelope, ListEnvelope } from "./models/common";
import Api from "./client";
import { pageSkip } from "./pageSkip";
import type { AxiosProgressEvent } from "axios";

/** Direct upload: multipart to the server, which answers with the Upload. */
export default new (class UploadApi {
  public directUpload(
    file: File,
    type: string,
    options?: {
      targetId?: string;
      onProgress?: (e: AxiosProgressEvent) => void;
      /**
       * Generated when absent. The server caches the answer for the same key
       * for an hour, so a retried upload makes no second record.
       */
      idempotencyKey?: string;
      /** A file of a post: who sees it, stored with the file. Every reader when absent. */
      audience?: UploadAudience;
    },
  ) {
    const formData = new FormData();
    formData.append("file", file);
    if (options?.audience) {
      appendAudience(formData, options.audience);
    }
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
   * Newest first. Without username the reader's own; with it, that user's,
   * which the server allows an admin only.
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
      skip: pageSkip(pageNumber, pageSize),
      take: pageSize,
    });
  }

  /** Who sees a file of a post, changed by its author; acts from the next request. */
  public updateAudience(id: string, audience: UploadAudience) {
    return Api.patch<Envelope<Upload>>(`uploads/${id}`, audienceBody(audience));
  }

  /** Soft delete, allowed to the owner and admins. */
  public deleteUpload(id: string, options?: Parameters<typeof Api.delete>[2]) {
    return Api.delete(`uploads/${id}`, undefined, options);
  }
})();

// The characters are read for "Selected" only, so "All" sends none.
function audienceBody(
  audience: UploadAudience,
): Pick<UploadAudience, "visibleTo"> & { characterIds?: string[] } {
  return audience.visibleTo === "Selected"
    ? { visibleTo: audience.visibleTo, characterIds: audience.characterIds }
    : { visibleTo: audience.visibleTo };
}

// The form repeats the field per character, as the server binds a list.
function appendAudience(formData: FormData, audience: UploadAudience) {
  const body = audienceBody(audience);
  formData.append("visibleTo", body.visibleTo);
  for (const id of body.characterIds ?? []) {
    formData.append("characterIds", id);
  }
}
