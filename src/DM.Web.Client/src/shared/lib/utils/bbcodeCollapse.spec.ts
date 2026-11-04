/**
 * @vitest-environment jsdom
 */

/**
 * The collapsible block the spoiler and the nsfw block stand on: a bare head
 * and its wrapper, with nothing of either.
 */

import { describe, it, expect, afterEach } from "vitest";
import { bindCollapseHead, wrapInCollapse } from "./bbcodeCollapse";
import {
  allExpanded,
  clearRegistry,
  collapseAll,
  expandAll,
  hasAny,
} from "@/shared/lib/composables/useExpandableRegistry";

describe("the collapsible block", () => {
  const unbinds: Array<() => void> = [];

  afterEach(() => {
    while (unbinds.length > 0) (unbinds.pop() as () => void)();
    document.body.innerHTML = "";
    clearRegistry();
  });

  function bind(): { head: HTMLElement; collapse: HTMLElement } {
    const host = document.createElement("div");
    host.innerHTML =
      '<a href="#" class="x-head">x</a><div class="x">текст</div>';
    document.body.appendChild(host);
    const head = host.querySelector(".x-head") as HTMLElement;
    const inner = host.querySelector(".x") as HTMLElement;
    const collapse = wrapInCollapse(inner, inner);
    unbinds.push(
      bindCollapseHead({
        head,
        collapse,
        kind: "x",
        headText: (open) => (open ? "закрыть" : "открыть"),
      }),
    );
    return { head, collapse };
  }

  const isOpen = (collapse: HTMLElement) => collapse.classList.contains("open");

  it("wraps the content where it stood, in a clip", () => {
    const host = document.createElement("div");
    host.innerHTML = '<p>до</p><div class="x">текст</div><p>после</p>';
    const inner = host.querySelector(".x") as HTMLElement;

    wrapInCollapse(inner, inner);

    expect(host.innerHTML).toBe(
      '<p>до</p><div class="bb-collapse"><div class="bb-collapse-clip">' +
        '<div class="x">текст</div></div></div><p>после</p>',
    );
  });

  it("is closed after binding, the head a button", () => {
    const { head, collapse } = bind();

    expect(isOpen(collapse)).toBe(false);
    expect(collapse.inert).toBe(true);
    expect(head.getAttribute("data-initialized")).toBe("true");
    expect(head.getAttribute("role")).toBe("button");
    expect(head.getAttribute("tabindex")).toBe("0");
    expect(head.getAttribute("aria-expanded")).toBe("false");
    expect(head.textContent).toBe("открыть");
  });

  it("opens and closes on a click, on Enter and on Space", () => {
    const { head, collapse } = bind();
    const key = (k: string) =>
      head.dispatchEvent(
        new KeyboardEvent("keydown", { key: k, cancelable: true }),
      );

    head.click();
    expect(isOpen(collapse)).toBe(true);
    expect(collapse.inert).toBe(false);
    expect(head.getAttribute("aria-expanded")).toBe("true");
    expect(head.textContent).toBe("закрыть");

    key("Enter");
    expect(isOpen(collapse)).toBe(false);

    key(" ");
    expect(isOpen(collapse)).toBe(true);

    key("a");
    expect(isOpen(collapse)).toBe(true);
  });

  it("follows Развернуть все and Свернуть все", () => {
    const { collapse } = bind();

    expandAll();
    expect(isOpen(collapse)).toBe(true);
    expect(allExpanded.value).toBe(true);

    collapseAll();
    expect(isOpen(collapse)).toBe(false);
  });

  it("opens a block that comes after Развернуть все", () => {
    expandAll();

    const { collapse } = bind();

    expect(isOpen(collapse)).toBe(true);
  });

  it("lets a manual toggle end the pending Развернуть все", () => {
    const { head } = bind();
    expandAll();
    head.click();

    const { collapse } = bind();

    expect(isOpen(collapse)).toBe(false);
  });

  it("lets go of the head and the registry on cleanup", () => {
    const { head, collapse } = bind();

    (unbinds.pop() as () => void)();
    head.click();

    expect(isOpen(collapse)).toBe(false);
    expect(hasAny.value).toBe(false);
  });
});
