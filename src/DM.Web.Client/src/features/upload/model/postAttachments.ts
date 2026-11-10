import type { PostAttachment } from "@/entities/game";
import { uploadApi } from "@/shared/api";
import { formatFileSize } from "@/shared/lib/utils/fileSize";
import type { PendingAttachment } from "./attachmentAudience";

// The server's numbers (UploadPolicy, ImageProcessingDefaults), restated so the picker refuses a
// file without a round trip. ClientFieldLimitsShould compares them.

const ALLOWED_MIME = [
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/gif",
] as const;

export const POST_ATTACHMENT_ACCEPT = ALLOWED_MIME.join(",");

// What a reader calls each format the server takes.
const FORMAT_NAMES = {
  "image/jpeg": "JPEG",
  "image/png": "PNG",
  "image/webp": "WebP",
  "image/gif": "GIF",
} satisfies Record<(typeof ALLOWED_MIME)[number], string>;

// "JPEG, PNG, WebP и GIF", in the order of the list the server is held to.
const FORMATS_IN_WORDS = (() => {
  const names = ALLOWED_MIME.map((type) => FORMAT_NAMES[type]);
  const last = names.pop();
  return names.length ? `${names.join(", ")} и ${last}` : (last ?? "");
})();

export const MAX_POST_ATTACHMENTS = 3;

export const MAX_POST_ATTACHMENT_BYTES = 5 * 1024 * 1024;

// The browser's guess of the type is a convenience: the server reads the magic bytes.
export function describeAttachmentProblem(file: File): string | null {
  if (!ALLOWED_MIME.some((type) => type === file.type)) {
    return `Допустимы только ${FORMATS_IN_WORDS}`;
  }
  if (file.size > MAX_POST_ATTACHMENT_BYTES) {
    return `Файл больше ${formatFileSize(MAX_POST_ATTACHMENT_BYTES)}`;
  }
  return null;
}

interface PickedAttachments {
  accepted: File[];
  error: string | null;
}

// The limit ends the pick; a file of the wrong kind or size is skipped, so the last refusal stands.
export function pickAttachments(
  chosen: File[],
  held: number,
): PickedAttachments {
  const accepted: File[] = [];
  let error: string | null = null;
  for (const file of chosen) {
    if (held + accepted.length >= MAX_POST_ATTACHMENTS) {
      error = `Не больше ${MAX_POST_ATTACHMENTS} файлов`;
      break;
    }
    const problem = describeAttachmentProblem(file);
    if (problem) {
      error = `${file.name}: ${problem}`;
      continue;
    }
    accepted.push(file);
  }
  return { accepted, error };
}

export interface AttachmentUploadResult {
  uploadedCount: number;
  failedNames: string[];
}

// Sequential: the server counts the post's attachments, and racing requests read the same count.
// The mark goes with the file, so it is never seen wider than chosen.
export async function uploadPostAttachments(
  postId: string,
  pending: readonly PendingAttachment[],
): Promise<AttachmentUploadResult> {
  const failedNames: string[] = [];
  let uploadedCount = 0;

  for (const { file, audience } of pending) {
    const { error } = await uploadApi.directUpload(file, "PostAttachment", {
      targetId: postId,
      audience,
    });
    if (error) {
      failedNames.push(file.name);
    } else {
      uploadedCount += 1;
    }
  }

  return { uploadedCount, failedNames };
}

// Matched by the upload id: the API gives a path, the text an absolute address.
const CONTENT_URL = /\/v1\/uploads\/([0-9a-fA-F-]{36})\/content(?:[?#]|$)/;

// Mirrors --bb-image-max-height in _BbcodeImage.sass: the width is capped so the pair keeps its ratio.
const DRAWN_HEIGHT_CAP = "500px";

// The server's type, read from the magic bytes, not the file name's claim.
export function isImageAttachment(attachment: PostAttachment): boolean {
  return attachment.contentType.startsWith("image/");
}

// Null without a usable measured pair: no box beats a guessed one.
function attachmentImageBox(
  attachment: PostAttachment,
): { width: number; height: number } | null {
  const { width, height } = attachment;
  if (!width || !height || width <= 0 || height <= 0) return null;
  return { width, height };
}

// The width and height pair lets the browser reserve the box before the bytes arrive.
// Left alone: external pictures, unmeasured attachments, author-sized frames, declared pairs.
export function reserveAttachmentImageBoxes(
  html: string,
  attachments: readonly PostAttachment[] | undefined,
): string {
  if (!html || !attachments?.length) return html;

  const sizes = attachmentImageBoxes(attachments);
  if (!sizes.size) return html;

  // A detached <template>: the live DOM is never touched.
  const template = document.createElement("template");
  template.innerHTML = html;

  let changed = false;
  for (const image of template.content.querySelectorAll("img")) {
    if (image.hasAttribute("width") && image.hasAttribute("height")) continue;
    if (image.parentElement?.classList.contains("bb-image-frame")) continue;
    const match = CONTENT_URL.exec(image.getAttribute("src") || "");
    if (!match) continue;
    const size = sizes.get(match[1].toLowerCase());
    if (!size) continue;
    image.setAttribute("width", String(size.width));
    image.setAttribute("height", String(size.height));
    image.style.setProperty("--bb-image-max-width", widthCap(size));
    changed = true;
  }

  return changed ? template.innerHTML : html;
}

// Keyed by the lowercased id, the form a content URL is matched in.
function attachmentImageBoxes(
  attachments: readonly PostAttachment[],
): Map<string, { width: number; height: number }> {
  const sizes = new Map<string, { width: number; height: number }>();
  for (const attachment of attachments) {
    const box = attachmentImageBox(attachment);
    if (!box) continue;
    sizes.set(attachment.id.toLowerCase(), box);
  }
  return sizes;
}

function widthCap(size: { width: number; height: number }): string {
  // Rounded down, so the height the width gives is never over the cap.
  const ratio = Math.floor((size.width / size.height) * 10000) / 10000;
  return `min(100%, calc(${DRAWN_HEIGHT_CAP} * ${ratio}))`;
}
