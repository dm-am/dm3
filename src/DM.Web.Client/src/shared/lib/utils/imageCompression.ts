/**
 * Client-side image compression — cuts the avatar down to 1024×1024 (max side)
 * before upload, saving bandwidth on mobile. Symmetric with the backend
 * OriginalMaxDimension=1024.
 *
 * No dependencies: pure Canvas API. WebP output if the browser supports it,
 * otherwise a JPEG fallback. Quality 90 — a size/visual balance.
 *
 * If the file is already under the size limits, it is returned as is (no-op).
 */

const MAX_DIMENSION = 1024;
const TARGET_QUALITY = 0.9;
const MIN_COMPRESS_THRESHOLD_BYTES = 200 * 1024; // <200 KB — do not compress

/**
 * Whether WebP encoding is supported in canvas.toBlob.
 * Test a 1×1 canvas → blob and read the mime. Cached per session.
 */
let webpSupportPromise: Promise<boolean> | null = null;
function isWebpSupported(): Promise<boolean> {
  if (webpSupportPromise) return webpSupportPromise;
  webpSupportPromise = new Promise((resolve) => {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 1;
    canvas.toBlob(
      (blob) => resolve(!!blob && blob.type === "image/webp"),
      "image/webp",
    );
  });
  return webpSupportPromise;
}

export interface CompressOptions {
  /** Maximum side. Defaults to 1024 (in sync with the backend). */
  maxDimension?: number;
  /** Quality 0..1 for lossy formats. Defaults to 0.9. */
  quality?: number;
}

/**
 * Compress an image via Canvas. Returns a new File with a .webp extension
 * (or a .jpg fallback) and the original base name. If the image is already
 * small (area ≤ MAX×MAX and size ≤ 200 KB), returns the original
 * File unchanged (no-op).
 */
export async function compressImage(
  file: File,
  options: CompressOptions = {},
): Promise<File> {
  const maxDim = options.maxDimension ?? MAX_DIMENSION;
  const quality = options.quality ?? TARGET_QUALITY;

  // Quick exit: leave small files alone — compression overhead > gains.
  if (file.size <= MIN_COMPRESS_THRESHOLD_BYTES) {
    return file;
  }

  if (!file.type.startsWith("image/")) {
    return file; // Not an image — do not compress.
  }

  const bitmap = await createImageBitmapSafe(file);
  if (!bitmap) return file;

  try {
    if (bitmap.width <= maxDim && bitmap.height <= maxDim) {
      return file; // Already fits the size limits.
    }

    // Resize, preserving the aspect ratio.
    const scale = Math.min(maxDim / bitmap.width, maxDim / bitmap.height);
    const w = Math.round(bitmap.width * scale);
    const h = Math.round(bitmap.height * scale);

    const canvas = document.createElement("canvas");
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext("2d");
    if (!ctx) return file;
    ctx.drawImage(bitmap, 0, 0, w, h);

    const useWebp = await isWebpSupported();
    const outType = useWebp ? "image/webp" : "image/jpeg";
    const outExt = useWebp ? ".webp" : ".jpg";

    const blob = await canvasToBlob(canvas, outType, quality);
    if (!blob || blob.size >= file.size) {
      // Compression is not worth it — return the original.
      return file;
    }

    const baseName = file.name.replace(/\.[^.]+$/, "");
    return new File([blob], `${baseName}${outExt}`, {
      type: outType,
      lastModified: Date.now(),
    });
  } finally {
    // close() exists only on ImageBitmap, not on HTMLImageElement.
    if ("close" in bitmap) bitmap.close();
  }
}

function canvasToBlob(
  canvas: HTMLCanvasElement,
  type: string,
  quality: number,
): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality));
}

/**
 * createImageBitmap with an <img>+canvas fallback for Safari, where
 * createImageBitmap may not support all formats.
 */
async function createImageBitmapSafe(
  file: File,
): Promise<ImageBitmap | HTMLImageElement | null> {
  try {
    if ("createImageBitmap" in window) {
      return await createImageBitmap(file);
    }
  } catch {
    // fall through
  }
  return await loadImageElement(file);
}

function loadImageElement(file: File): Promise<HTMLImageElement | null> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = () => {
      URL.revokeObjectURL(url);
      resolve(null);
    };
    img.src = url;
  });
}
