/**
 * @vitest-environment jsdom
 */

// The element an address names is brought into view and marked a moment after
// the render, and the component that asked can be gone before the moment is
// over: what stands under the same name by then belongs to another page.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";
import { flushPromises } from "@vue/test-utils";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useCommentHashScroll, useScrollToElement } from "./useScrollToElement";

// The moment after the render is a timer: the tests say when it runs out.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["setTimeout"] });
});

afterEach(() => {
  vi.useRealTimers();
  delete (Element.prototype as Partial<Element>).scrollIntoView;
  document.body.innerHTML = "";
});

/** jsdom lays nothing out and has no scrollIntoView of its own. */
function recordScrolls(): Element[] {
  const scrolled: Element[] = [];
  Element.prototype.scrollIntoView = function (this: Element) {
    scrolled.push(this);
  };
  return scrolled;
}

/**
 * At `address`, a component that asks for the reveal and draws the element it
 * names, under a shell that can take it away while the address stays.
 */
async function mountAsking(
  address: string,
  ask: () => void,
  named: Record<string, string>,
) {
  const shown = ref(true);
  const Asking = defineComponent({
    setup() {
      ask();
      return () => h("div", named);
    },
  });
  const Shell = defineComponent({
    setup: () => () => (shown.value ? h(Asking) : null),
  });
  const { router } = await mountPage(Shell, address, {
    routes: [["thread", "/thread"]],
    attachTo: document.body,
  });
  await flushPromises();
  const goAway = async () => {
    shown.value = false;
    await nextTick();
  };
  return { router, goAway };
}

/** An element of another page, under the name the address gives. */
function stranger(named: Record<string, string>): HTMLElement {
  const element = document.createElement("div");
  for (const [name, value] of Object.entries(named))
    element.setAttribute(name, value);
  return document.body.appendChild(element);
}

describe("useCommentHashScroll", () => {
  const COMMENT = { id: "comment-7" };
  const askForComment = () => useCommentHashScroll(() => [{ id: "7" }]);

  it("brings the comment the address names into view and marks it", async () => {
    const scrolled = recordScrolls();
    await mountAsking("/thread#comment-7", askForComment, COMMENT);

    await vi.advanceTimersByTimeAsync(100);

    expect(scrolled.map((element) => element.id)).toEqual(["comment-7"]);
    expect(scrolled[0].classList.contains("highlight-unread")).toBe(true);
  });

  it("leaves the page alone once the comments are gone", async () => {
    const scrolled = recordScrolls();
    const { goAway } = await mountAsking(
      "/thread#comment-7",
      askForComment,
      COMMENT,
    );

    await goAway();
    const other = stranger(COMMENT);
    await vi.advanceTimersByTimeAsync(300);

    expect(scrolled).toEqual([]);
    expect(other.classList.contains("highlight-unread")).toBe(false);
  });
});

describe("useScrollToElement", () => {
  const POST = { "data-id": "p-7" };
  const askForPost = () => useScrollToElement(ref(true));

  it("brings the element the address names into view and drops the name", async () => {
    const scrolled = recordScrolls();
    const { router } = await mountAsking(
      "/thread?scrollTo=p-7",
      askForPost,
      POST,
    );

    await vi.advanceTimersByTimeAsync(100);
    await flushPromises();

    expect(scrolled.map((element) => element.getAttribute("data-id"))).toEqual([
      "p-7",
    ]);
    expect(router.currentRoute.value.fullPath).toBe("/thread");
  });

  it("leaves the page and its address alone once the posts are gone", async () => {
    const scrolled = recordScrolls();
    const { router, goAway } = await mountAsking(
      "/thread?scrollTo=p-7",
      askForPost,
      POST,
    );

    await goAway();
    const other = stranger(POST);
    await vi.advanceTimersByTimeAsync(300);
    await flushPromises();

    expect(scrolled).toEqual([]);
    expect(other.classList.contains("highlight-unread")).toBe(false);
    expect(router.currentRoute.value.fullPath).toBe("/thread?scrollTo=p-7");
  });
});
