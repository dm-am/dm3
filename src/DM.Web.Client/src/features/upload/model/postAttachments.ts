import type { PostAttachment } from "@/entities/game";
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

/**
 * Address of the content endpoint, matched by the upload it names rather than
 * by the whole string.
 *
 * The API hands the attachment over as the path alone, while a picture the
 * author put in the text carries whatever they pasted — the BBCode sanitizer
 * passes http and https only, so in the text it is always absolute. Both
 * spellings name the same upload, and the identifier is the part that says so.
 */
const CONTENT_URL = /\/v1\/uploads\/([0-9a-fA-F-]{36})\/content(?:[?#]|$)/;

/**
 * Tallest a picture in BBCode text is drawn: 500 pixels, as on DM2. Mirrors
 * the `--bb-image-max-height` fallback in `_BbcodeContent.sass`, which is
 * where it is decided.
 *
 * Needed here because the cap and a declared width interact: the browser
 * clamps the height against it without touching the width, so a picture
 * taller than the cap would keep the full width of its pair and be drawn
 * narrower inside a box that is not (object-fit), with the gap on both sides.
 * The pair stays the file's own, and the width is capped instead, at the width
 * the picture has when it is exactly as tall as the cap; the height follows
 * the ratio down to the cap by itself.
 */
const DRAWN_HEIGHT_CAP = "500px";

/**
 * Whether the attachment is a picture, which is what decides that a post shows
 * it instead of only naming it.
 *
 * The type is the server's answer and not the file name's claim: the upload
 * pipeline reads the magic bytes and stores what the file turned out to be, so
 * a document renamed to .png is a document here too. Same test the upload
 * tables use, spelled for the attachment payload.
 */
export function isImageAttachment(attachment: PostAttachment): boolean {
  return attachment.contentType.startsWith("image/");
}

/**
 * The box a picture attachment is measured at, or null when its size is
 * unknown.
 *
 * The pair is what reserves room before the bytes arrive: the file's own
 * width and height, as the upload pipeline measured them. Null for an
 * attachment stored before the pipeline recorded a size, and for a pair that
 * is not a usable ratio: no box beats a guessed one.
 */
export function attachmentImageBox(
  attachment: PostAttachment,
): { width: number; height: number } | null {
  const { width, height } = attachment;
  if (!width || !height || width <= 0 || height <= 0) return null;
  return { width, height };
}

/**
 * Declare the box for every picture in `html` that is one of `attachments`.
 *
 * The point is the pair of attributes and nothing else: the browser derives an
 * aspect ratio from width and height and reserves the box from it before a byte
 * of the picture has arrived, so the text below it is laid out once instead of
 * being pushed down when the image decodes. Because what it reserves is a
 * ratio, `max-width: 100%` keeps working — the box shrinks with the column
 * rather than staying at the pixel size the file happens to have, and the
 * reservation is right at every width rather than at the file's own.
 *
 * With the pair goes a cap on the width (`--bb-image-max-width`, which the
 * stylesheet already reads for every picture of the text): the width at which
 * the picture is exactly as tall as the text allows (DRAWN_HEIGHT_CAP). A
 * tall map is then drawn smaller as a whole, the way a picture without a pair
 * is, and a click on it opens it whole like any other picture of the text
 * (bbcodeInteractive.ts, shared/ui/ImageViewer). A collapsed post caps its
 * pictures with a declaration of its own and does not read the property.
 *
 * Left alone, deliberately:
 *   - any address that is not this post's own attachment, which includes every
 *     external picture in the text — nothing here knows how big those are, and
 *     a guess would trade a shift for a wrong box;
 *   - an attachment stored before the pipeline recorded its size, and one whose
 *     pair is not a usable ratio;
 *   - a picture the author gave a size of their own, which is wrapped in
 *     `.bb-image-frame` and drawn to the caps that carries — a second opinion
 *     about its box would fight the first;
 *   - an element that already declares both attributes.
 */
export function reserveAttachmentImageBoxes(
  html: string,
  attachments: readonly PostAttachment[] | undefined,
): string {
  if (!html || !attachments?.length) return html;
  // Parsing costs more than the scan that decides it is pointless, and a post
  // carrying an attachment picture is the rare one.
  if (!html.includes("/v1/uploads/")) return html;

  const sizes = new Map<string, { width: number; height: number }>();
  for (const attachment of attachments) {
    const box = attachmentImageBox(attachment);
    if (!box) continue;
    sizes.set(attachment.id.toLowerCase(), box);
  }
  if (!sizes.size) return html;

  // A detached <template>, like the whitespace trim next to which this runs:
  // the live DOM is never touched and the caller stays a pure computed.
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

/**
 * The widest a picture of this ratio is drawn: the column, or the width at
 * which it is exactly as tall as DRAWN_HEIGHT_CAP, whichever is narrower.
 */
function widthCap(size: { width: number; height: number }): string {
  // Rounded down, so the height the width gives is never over the cap.
  const ratio = Math.floor((size.width / size.height) * 10000) / 10000;
  return `min(100%, calc(${DRAWN_HEIGHT_CAP} * ${ratio}))`;
}
