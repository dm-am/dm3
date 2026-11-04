// The preview, extension and shared columns of both upload tables; the pages
// keep what differs.
import type { Column } from "@/shared/ui/DataTable";
import type { Upload } from "@/shared/api/models/common/upload";
import { apiUrl } from "@/shared/api/client";

/** Whether the upload is an image (drives thumbnail vs. extension preview). */
export function isImage(upload: Upload): boolean {
  return upload.contentType.startsWith("image/");
}

/**
 * The public URL where the type has one, otherwise the authorizing content
 * endpoint: a post attachment has no public URL.
 */
export function uploadHref(upload: Upload): string {
  return upload.url ?? apiUrl(upload.contentUrl);
}

/**
 * Extension label shown for non-image previews (e.g. "PDF", "ZIP"), capped
 * at five characters; falls back to "Файл" when there is no usable extension.
 */
export function fileExt(upload: Upload): string {
  const name = upload.originalFileName;
  const dot = name.lastIndexOf(".");
  if (dot > 0 && dot < name.length - 1) {
    return name
      .slice(dot + 1)
      .toUpperCase()
      .slice(0, 5);
  }
  return "Файл";
}

/** Thumbnail/extension preview column, identical across upload tables. */
export const uploadPreviewColumn: Column = {
  key: "preview",
  label: "Превью",
  width: "72px",
  align: "center",
};

/** File-name column, identical across upload tables: the main one. */
export const uploadFileColumn: Column = { key: "file", label: "Файл" };
