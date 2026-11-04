import { readonly, ref } from "vue";

/**
 * The picture of a text open whole over the page, or null: the text hears the
 * click (bbcodeImageViewer.ts) and hands it to the viewer the app draws once.
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
