// Shared helpers for the upload tables (profile "Загруженное" and moderation
// "Все загруженное"). SSOT for the preview/extension logic and the column
// specs both tables share; page-specific columns (uploader, widths) stay in
// the pages since they legitimately differ.
import type { Column } from "@/shared/ui/DataTable";
import type { Upload } from "@/shared/api/models/common/upload";

/** Whether the upload is an image (drives thumbnail vs. extension preview). */
export function isImage(upload: Upload): boolean {
  return upload.contentType.startsWith("image/");
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

/** File-name column, identical across upload tables. */
export const uploadFileColumn: Column = { key: "file", label: "Файл" };
