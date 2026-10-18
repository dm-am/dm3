import { readonly, ref } from "vue";

/**
 * The picture of a text open whole over the page, or null.
 *
 * A picture in a post is drawn no taller than the window allows, and a map or
 * a character sheet taller than that cannot be read there. A click opens it at
 * its own size in the viewer (shared/ui/ImageViewer), which the app draws
 * once; the click itself is heard by the text (bbcodeInteractive.ts), which
 * knows nothing of the component and hands the picture over through here.
 */
export interface ViewedImage {
  src: string;
}

const viewed = ref<ViewedImage | null>(null);

/** What the viewer shows; null while it is closed. */
export const viewedImage = readonly(viewed);

export function openImageViewer(src: string): void {
  viewed.value = { src };
}

export function closeImageViewer(): void {
  viewed.value = null;
}
