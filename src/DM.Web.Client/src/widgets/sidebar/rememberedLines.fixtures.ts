/**
 * A layout for the remembered skeleton of a viewer's own block, which jsdom
 * does not lay out: the list's rows end `lines` lines of 16px text below the
 * first row, as when a long title wraps.
 */
import { vi } from "vitest";

const FONT_SIZE = 16;
// The skeleton row's 1.3em (SidebarSkeleton).
const LINE = FONT_SIZE * 1.3;

export function layOutRowsAs(lines: number) {
  vi.spyOn(Element.prototype, "getBoundingClientRect").mockImplementation(
    function (this: Element) {
      const first = this.parentElement?.firstElementChild === this;
      return { top: first ? 0 : lines * LINE } as DOMRect;
    },
  );
  const computed = window.getComputedStyle;
  vi.spyOn(window, "getComputedStyle").mockImplementation((element) =>
    Object.defineProperty(Object.create(computed(element)), "fontSize", {
      value: `${FONT_SIZE}px`,
    }),
  );
}
