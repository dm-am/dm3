import { uploadApi } from "@/shared/api";
import { formatFileSize } from "@/shared/lib/utils/fileSize";

/**
 * Attaching files to a post, and the rules the composer enforces before it
 * bothers the server.
 *
 * The numbers and the format list are the server's — UploadPolicy and
 * ImageProcessingDefaults.PostAttachmentContentTypes — restated here so the
 * picker can refuse a file without a round trip. The server refuses the same
 * things regardless; nothing below is a permission check.
 */

/** Formats a post attachment may be in. Mirrors the server allow-list. */
export const POST_ATTACHMENT_ACCEPT =
  "image/jpeg,image/png,image/webp,image/gif";

const ALLOWED_MIME = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/gif",
]);

/** How many files one post may carry (UploadPolicy.MaxPostAttachments). */
export const MAX_POST_ATTACHMENTS = 3;

/** Largest file a post accepts (UploadPolicy.MaxPostAttachmentSizeBytes). */
export const MAX_POST_ATTACHMENT_BYTES = 5 * 1024 * 1024;

/**
 * Why a chosen file cannot be attached, or null when it can.
 *
 * The type is checked against the browser's guess, which is a convenience and
 * not the decision: the server reads the magic bytes and refuses a renamed file
 * whatever this said.
 */
export function describeAttachmentProblem(file: File): string | null {
  if (!ALLOWED_MIME.has(file.type)) {
    return "Допустимы только JPEG, PNG, WebP и GIF";
  }
  if (file.size > MAX_POST_ATTACHMENT_BYTES) {
    return `Файл больше ${formatFileSize(MAX_POST_ATTACHMENT_BYTES)}`;
  }
  return null;
}

/** Result of attaching a batch of files to one post. */
export interface AttachmentUploadResult {
  /** Files that reached the server and became attachments. */
  uploadedCount: number;
  /** Names of the files that did not, in the order they were tried. */
  failedNames: string[];
}

/**
 * Upload each file as an attachment of an existing post.
 *
 * Sequential on purpose. The server counts the post's attachments to enforce
 * the limit, and three requests racing each other read the same count; one at a
 * time, the third of four is refused rather than accepted by a lost race.
 *
 * Partial success is a real outcome and is reported rather than hidden: the
 * post is already published by the time this runs — it has to be, because an
 * attachment names the post it belongs to — so a failure here leaves a post
 * without its file, and the caller has to say so.
 */
export async function uploadPostAttachments(
  postId: string,
  files: File[],
): Promise<AttachmentUploadResult> {
  const failedNames: string[] = [];
  let uploadedCount = 0;

  for (const file of files) {
    const { error } = await uploadApi.directUpload(file, "PostAttachment", {
      targetId: postId,
    });
    if (error) {
      failedNames.push(file.name);
    } else {
      uploadedCount += 1;
    }
  }

  return { uploadedCount, failedNames };
}
