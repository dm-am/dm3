/**
 * @vitest-environment jsdom
 *
 * TruncatedContent — line-box snapped clamp regression suite.
 *
 * THE INVARIANT UNDER TEST: the collapsed hard cut must never slice a text
 * line in half. `maxHeight` is a budget; the applied clamp is snapped DOWN to
 * the bottom of the last REAL line box that fits within the budget — measured
 * from the rendered line boxes, NOT from an assumed uniform grid.
 *
 * These tests exist because a uniform-grid clamp (offsetTop + n * lineHeight)
 * silently sliced the last visible line when inter-paragraph margins or mixed
 * line-heights shifted the real lines off the assumed grid (a 150px budget
 * snapped to 140 while the real line ran 130-150 -> 10px peek).
 *
 * jsdom performs no layout, so each mount mocks Range.prototype.getClientRects
 * to feed the component a synthetic set of line boxes. The content box is
 * pinned to top: 0 (jsdom's getBoundingClientRect default), so each rect's
 * `bottom` is exactly the y-from-the-box-top the component reads.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import TruncatedContent from "./TruncatedContent.vue";
import { registerExpandable } from "@/shared/lib/composables/useExpandableRegistry";

// The registry is a module-scoped singleton every mounted expandable shares.
// Stub the named imports the component uses.
vi.mock("@/shared/lib/composables/useExpandableRegistry", () => ({
  registerExpandable: vi.fn(() => vi.fn()),
  notifyExpandableChanged: vi.fn(),
  refreshExpandableStates: vi.fn(),
}));

const registerExpandableMock = vi.mocked(registerExpandable);

const originalGetClientRects = Range.prototype.getClientRects;
afterEach(() => {
  Range.prototype.getClientRects = originalGetClientRects;
});

function mockScrollHeight(el: Element, value: number): void {
  Object.defineProperty(el, "scrollHeight", { value, configurable: true });
}

/** One synthetic line box: `bottom` is its y from the content-box top. */
type Line = { bottom: number; height?: number };

/** Uniform grid of `count` line boxes of `lineHeight`, first line's top at
 * `start`. */
function grid(count: number, lineHeight: number, start = 0): Line[] {
  return Array.from({ length: count }, (_, i) => ({
    bottom: start + (i + 1) * lineHeight,
    height: lineHeight,
  }));
}

interface MountOptions {
  /** Mocked natural content height (jsdom performs no layout). */
  scrollHeight: number;
  /** Synthetic line boxes fed to Range.getClientRects. Empty = no measurable
   * text (media-only / no layout) — the raw budget applies. */
  lines: Line[];
  maxHeight?: number;
}

async function mountTruncated({
  scrollHeight,
  lines,
  maxHeight = 150,
}: MountOptions) {
  const rects = lines.map((l) => ({
    top: l.bottom - (l.height ?? 20),
    bottom: l.bottom,
    height: l.height ?? 20,
    width: 120,
  })) as unknown as DOMRectList;
  // Set BEFORE mount — the line measurement runs during mount's nextTick.
  Range.prototype.getClientRects = function () {
    return rects;
  };

  const wrapper = mount(TruncatedContent, {
    props: { truncatable: true, maxHeight },
    slots: { default: `<div>line-snapped content</div>` },
  });
  const content = wrapper.find(".truncated-content").element as HTMLElement;
  mockScrollHeight(content, scrollHeight);
  await flushPromises();
  await nextTick();
  return { wrapper, content };
}

function expandButton(wrapper: ReturnType<typeof mount>) {
  return wrapper.find(".truncated-expand-button");
}

describe("TruncatedContent line-box snapping", () => {
  it("does not truncate content that fits the budget", async () => {
    // 5 lines, all within the 150px budget — nothing overflows.
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 100,
      lines: grid(5, 20),
    });
    expect(expandButton(wrapper).exists()).toBe(false);
    expect(content.style.maxHeight).toBe("");
  });

  it("snaps the collapsed clamp to the last line that fits (uniform grid)", async () => {
    // 20px lines, 150px budget: line 8 runs 140-160 (overflows), so the clamp
    // stops at line 7's bottom = 140.
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 400,
      lines: grid(20, 20),
    });
    expect(expandButton(wrapper).exists()).toBe(true);
    expect(content.style.maxHeight).toBe("140px");
    expect(content.style.getPropertyValue("--truncated-max-h")).toBe("140px");
  });

  it("snaps past an inter-block margin without slicing a line (the peek regression)", async () => {
    // The real bug: a paragraph margin shifts later lines off the first-line
    // grid. Lines: 6 x 20px (bottoms 20..120), then a 10px margin gap, then
    // the next paragraph runs 130-150, 150-170. A uniform-grid clamp would
    // stop at 140 and slice the 130-150 line in half (10px peek). The real
    // line box at 130-150 fits the 150px budget, so the clamp must be 150.
    const lines: Line[] = [...grid(6, 20), { bottom: 150 }, { bottom: 170 }];
    const { content } = await mountTruncated({ scrollHeight: 400, lines });
    expect(content.style.maxHeight).toBe("150px");
  });

  it("adapts to the measured line-height (24px lines -> 144px)", async () => {
    // floor budget onto real 24px line boxes: last fitting bottom = 144.
    const { content } = await mountTruncated({
      scrollHeight: 400,
      lines: grid(10, 24),
    });
    expect(content.style.maxHeight).toBe("144px");
  });

  it("does not truncate for a sub-line overflow beyond the last line", async () => {
    // 7 lines end at 140 (all within budget); the natural height is 155px
    // (a trailing margin), but there is no NEXT line box — truncating would
    // hide only whitespace behind the button. Must render fully.
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 155,
      lines: grid(7, 20),
    });
    expect(expandButton(wrapper).exists()).toBe(false);
    expect(content.style.maxHeight).toBe("");
  });

  it("truncates when a whole line lies beyond the clamp", async () => {
    // 8 lines (bottoms 20..160): line 8 (140-160) overflows the 150 budget,
    // clamp = line 7 bottom = 140.
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 160,
      lines: grid(8, 20),
    });
    expect(expandButton(wrapper).exists()).toBe(true);
    expect(content.style.maxHeight).toBe("140px");
  });

  it("falls back to the raw budget when there is no measurable text", async () => {
    // Media-only content / no layout: no line boxes -> raw budget clamp.
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 400,
      lines: [],
    });
    expect(expandButton(wrapper).exists()).toBe(true);
    expect(content.style.maxHeight).toBe("150px");
  });

  it("re-snaps when the budget prop changes", async () => {
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 400,
      lines: grid(20, 20),
    });
    expect(content.style.maxHeight).toBe("140px");
    // 90px budget on the 20px grid: last fitting line bottom = 80.
    await wrapper.setProps({ maxHeight: 90 });
    await flushPromises();
    await nextTick();
    expect(content.style.maxHeight).toBe("80px");
  });

  it("expands to the natural height and collapses back to the snapped clamp", async () => {
    const { wrapper, content } = await mountTruncated({
      scrollHeight: 400,
      lines: grid(20, 20),
    });

    // Expand: animation target is the natural (scroll) height.
    await expandButton(wrapper).trigger("click");
    await flushPromises();
    await nextTick();
    expect(content.style.maxHeight).toBe("400px");
    expect(expandButton(wrapper).exists()).toBe(false);

    // End of the expand transition releases the constraint entirely.
    const expandEnd = new Event("transitionend");
    Object.defineProperty(expandEnd, "propertyName", { value: "max-height" });
    content.dispatchEvent(expandEnd);
    await nextTick();
    expect(content.style.maxHeight).toBe("none");

    // Collapse (via the expand-all registry handle, as ScrollNav does):
    // the animation target is the SNAPPED clamp, not the raw budget.
    const handle = registerExpandableMock.mock.calls.at(-1)?.[0];
    expect(handle).toBeDefined();
    handle?.collapse();
    await flushPromises();
    await nextTick();
    expect(content.style.maxHeight).toBe("140px");
  });
});
