/**
 * File upload (server response).
 *
 * Direct upload flow only — the server pipeline validates and processes the file,
 * the client receives a ready Upload with the source file URL. Thumbnail variants
 * are generated on-the-fly via imgproxy at serving time — they are available
 * via the picture object in the User/UserProfile/Character DTOs (small/medium URLs).
 */
export interface Upload {
  id: string;
  userId: string;
  /**
   * Owner username (profile ref). Present on the moderation list views so the
   * "Загрузил" column can link to the uploader profile; absent on self paths.
   */
  uploaderUsername?: string;
  type: string;
  targetId?: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  status: "Pending" | "Confirmed" | "Failed";
  /** Source file URL (≤1024 px, EXIF stripped). */
  url?: string;
  createdUtc: string;
  confirmedUtc?: string;
}
