/**
 * @vitest-environment jsdom
 */

/**
 * The count and the give-back of the lock on `.main`: a question over a window
 * keeps the page held, and a value written over the lock meanwhile stays.
 */
import { afterEach, describe, it, expect } from "vitest";
import { defineComponent } from "vue";
import { mount } from "@vue/test-utils";
import { setScrollContainer } from "@/shared/lib/scroll";
import { usePageScrollLock } from "./usePageScrollLock";

/** A holder the test drives by hand, as a window does by opening and closing. */
const holder = () => {
  let lock!: ReturnType<typeof usePageScrollLock>;
  const wrapper = mount(
    defineComponent({
      setup() {
        lock = usePageScrollLock();
        return () => null;
      },
    }),
  );
  return { lock, wrapper };
};

const page = () => {
  const el = document.createElement("div");
  setScrollContainer(el);
  return el;
};

const gutter = (el: HTMLElement) =>
  el.style.getPropertyValue("scrollbar-gutter");

afterEach(() => setScrollContainer(null));

describe("the page under a window", () => {
  it("is held, with the place of its scrollbar kept, and let go", () => {
    const el = page();
    const { lock, wrapper } = holder();

    lock.hold();
    expect(el.style.overflow).toBe("hidden");
    expect(gutter(el)).toBe("stable");

    lock.release();
    expect(el.style.overflow).toBe("");
    expect(gutter(el)).toBe("");
    wrapper.unmount();
  });

  it("stays held until the last of two windows closes", () => {
    const el = page();
    const window = holder();
    const question = holder();

    window.lock.hold();
    question.lock.hold();
    question.lock.release();
    expect(el.style.overflow).toBe("hidden");

    window.lock.release();
    expect(el.style.overflow).toBe("");
    window.wrapper.unmount();
    question.wrapper.unmount();
  });

  it("counts a holder once however often it holds", () => {
    const el = page();
    const { lock, wrapper } = holder();

    lock.hold();
    lock.hold();
    lock.release();

    expect(el.style.overflow).toBe("");
    wrapper.unmount();
  });

  it("lets go when a holder leaves while holding", () => {
    const el = page();
    const { lock, wrapper } = holder();

    lock.hold();
    wrapper.unmount();

    expect(el.style.overflow).toBe("");
  });

  it("leaves a value written over the lock while the page was held", () => {
    const el = page();
    el.style.overflow = "hidden"; // held by somebody else
    const { lock, wrapper } = holder();

    lock.hold();
    el.style.overflow = ""; // let go by them under the window
    lock.release();

    expect(el.style.overflow).toBe("");
    expect(gutter(el)).toBe("");
    wrapper.unmount();
  });
});
