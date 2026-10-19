import { openImageViewer } from "@/shared/lib/composables/useImageViewer";
import { registerCleanup } from "./bbcodeCleanup";

/** Containers that already hear a click on their pictures. */
const imageListeners = new WeakSet<HTMLElement>();

/** A picture of the text that opens whole. */
function isTextPicture(target: EventTarget | null): target is HTMLImageElement {
  return target instanceof HTMLImageElement && target.matches(".bb-image");
}

/**
 * A click on a picture of the text opens it whole over the page (shared/ui/
 * ImageViewer). One listener per container, so pictures that arrive later open
 * too and a re-init piles nothing up. A picture inside [link] is the link's; a
 * press with a modifier is the browser's.
 */
export function initImageViewer(container: HTMLElement): void {
  if (imageListeners.has(container)) return;

  const onClick = (event: MouseEvent) => {
    if (event.defaultPrevented || event.button !== 0) return;
    if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
      return;
    }
    const target = event.target;
    if (!isTextPicture(target) || !container.contains(target)) return;
    if (target.closest("a[href]")) return;

    event.preventDefault();
    openImageViewer(target.currentSrc || target.src);
  };

  container.addEventListener("click", onClick);
  imageListeners.add(container);
  registerCleanup(container, () => {
    container.removeEventListener("click", onClick);
    imageListeners.delete(container);
  });
}
