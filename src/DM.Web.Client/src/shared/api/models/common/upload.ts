/**
 * File upload (server response).
 *
 * Direct upload flow only — серверный pipeline валидирует и процессит файл,
 * клиент получает готовый Upload с URL source-файла. Thumbnail-варианты
 * генерируются on-the-fly через imgproxy при serving — они доступны
 * через picture-объект в User/UserProfile/Character DTO (small/medium URLs).
 */
export interface Upload {
  id: string;
  userId: string;
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
