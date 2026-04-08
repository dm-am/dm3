export interface Upload {
  id: string;
  userId: string;
  type: string;
  targetId?: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: "Pending" | "Confirmed" | "Failed";
  originalUrl?: string;
  mediumUrl?: string;
  smallUrl?: string;
  createdUtc: string;
  confirmedUtc?: string;
}

export interface PresignRequest {
  type: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  targetId?: string;
}

export interface PresignResponse {
  uploadId: string;
  presignedUrl: string;
  expiresUtc: string;
}
