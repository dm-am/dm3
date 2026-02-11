import type { Envelope } from "@/api/models/common";
import type { Upload, PresignRequest, PresignResponse } from "@/api/models/common/upload";
import Api from "@/api";
import type { AxiosProgressEvent } from "axios";

export default new (class UploadApi {
  /**
   * Direct upload: file -> server processing -> confirmed Upload.
   * For small files (avatars, portraits) that need server-side image processing.
   */
  public directUpload(
    file: File,
    type: string,
    options?: {
      targetId?: string;
      onProgress?: (e: AxiosProgressEvent) => void;
    },
  ) {
    const formData = new FormData();
    formData.append("file", file);
    const query = new URLSearchParams({ type });
    if (options?.targetId) {
      query.set("targetId", options.targetId);
    }
    return Api.postFile<Envelope<Upload>>(
      `uploads/direct?${query.toString()}`,
      formData,
      options?.onProgress,
    );
  }

  /**
   * Presigned URL workflow: for large files uploaded directly to S3.
   */
  public requestPresignedUrl(request: PresignRequest) {
    return Api.post<Envelope<PresignResponse>>("uploads/presign", request);
  }

  public confirmUpload(uploadId: string) {
    return Api.post<Envelope<Upload>>(`uploads/${uploadId}/confirm`);
  }

  public getUpload(uploadId: string) {
    return Api.get<Envelope<Upload>>(`uploads/${uploadId}`);
  }

  public deleteUpload(uploadId: string) {
    return Api.delete(`uploads/${uploadId}`);
  }
})();
