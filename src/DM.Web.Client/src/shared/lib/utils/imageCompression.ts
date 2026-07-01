/**
 * Client-side image compression — режет аватар до 1024×1024 (max-side)
 * перед upload'ом, экономит bandwidth на mobile. Symmetric с backend
 * OriginalMaxDimension=1024.
 *
 * Без зависимостей: чистый Canvas API. WebP output если браузер поддерживает,
 * иначе fallback на JPEG. Качество 90 — баланс размера и визуала.
 *
 * Если файл уже меньше лимита по размерам — возвращаем как есть (no-op).
 */

const MAX_DIMENSION = 1024;
const TARGET_QUALITY = 0.9;
const MIN_COMPRESS_THRESHOLD_BYTES = 200 * 1024; // <200 KB — не сжимаем

/**
 * Поддерживается ли WebP encoding в canvas.toBlob.
 * Тестируем 1×1 canvas → blob и читаем mime. Кешируется per-session.
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
  /** Максимальная сторона. По умолчанию 1024 (sync с backend). */
  maxDimension?: number;
  /** Quality 0..1 для lossy форматов. По умолчанию 0.9. */
  quality?: number;
}

/**
 * Сжать изображение через Canvas. Возвращает новый File с расширением .webp
 * (или .jpg fallback) и оригинальным base-name. Если изображение и так
 * маленькое (площадь ≤ MAX×MAX и размер ≤ 200 KB), возвращает оригинальный
 * File без изменений (no-op).
 */
export async function compressImage(
  file: File,
  options: CompressOptions = {},
): Promise<File> {
  const maxDim = options.maxDimension ?? MAX_DIMENSION;
  const quality = options.quality ?? TARGET_QUALITY;

  // Quick exit: маленькие файлы не трогаем — overhead compression > выигрыш.
  if (file.size <= MIN_COMPRESS_THRESHOLD_BYTES) {
    return file;
  }

  if (!file.type.startsWith("image/")) {
    return file; // Не-image — не сжимаем.
  }

  const bitmap = await createImageBitmapSafe(file);
  if (!bitmap) return file;

  try {
    if (bitmap.width <= maxDim && bitmap.height <= maxDim) {
      return file; // Уже подходит по размерам.
    }

    // Resize, сохраняем aspect ratio.
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
      // Сжатие не выгодно — возвращаем оригинал.
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
 * createImageBitmap с fallback на <img>+canvas для Safari, где
 * createImageBitmap может не поддерживать все форматы.
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
