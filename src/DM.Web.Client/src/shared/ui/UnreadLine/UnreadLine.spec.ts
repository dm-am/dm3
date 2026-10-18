/**
 * @vitest-environment jsdom
 */

/**
 * The line under a heading that marks everything read at once.
 *
 * It took the place of two looks of one action: a large grey "Прочитать все"
 * beside the heading of the notifications, wider than the heading row was
 * high and silent about how much there was to read, and a shaded "Отметить все
 * как прочитанное" over the forum's table, offered with nothing to mark. The
 * line counts what is unread, stands only while there is any, and after a
 * press puts the reader on the heading, since the link they pressed goes with
 * the line.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, ref } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import type { GeneralError } from "@/shared/api/models/common";
import { componentCss } from "@/shared/lib/testing/styles";
import UnreadLine from "./UnreadLine.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

const refused = (status: number, title = ""): GeneralError => ({
  type: "about:blank",
  title,
  status,
  traceId: "t",
});

let mounted: VueWrapper | null = null;

afterEach(() => {
  mounted?.unmount();
  mounted = null;
  document.body.innerHTML = "";
});

/**
 * The line under an h1, as a page draws it, with the count the page owns:
 * a successful mark takes it to zero, as the pages do.
 */
function render(
  count: number,
  mark: (count: { value: number }) => Promise<GeneralError | null> = async (
    c,
  ) => {
    c.value = 0;
    return null;
  },
) {
  const shown = ref(count);
  const markSpy = vi.fn(() => mark(shown));
  const Page = defineComponent({
    setup: () => () =>
      h("main", [
        h("h1", "Уведомления"),
        h(UnreadLine, {
          count: shown.value,
          mark: markSpy,
          failure: "Не удалось отметить уведомления",
          done: "Все уведомления прочитаны",
        }),
      ]),
  });
  const wrapper = mount(Page, { attachTo: document.body });
  mounted = wrapper;
  return { wrapper, shown, markSpy };
}

/** The line as a reader copies it: runs of white space as one space. */
const lineText = (wrapper: VueWrapper) => {
  const line = wrapper.find(".unread-line");
  if (!line.exists()) return null;
  return line.element.textContent!.replace(/\s+/g, " ").trim();
};

describe("the line of the unread", () => {
  it("is not there while nothing is unread", () => {
    const { wrapper } = render(0);

    expect(wrapper.find(".unread-line").exists()).toBe(false);
    expect(wrapper.find("button").exists()).toBe(false);
  });

  it("counts what is unread and offers to mark it", () => {
    const { wrapper } = render(4);

    expect(lineText(wrapper)).toBe("Непрочитанных: 4 | Отметить прочитанными");
    expect(wrapper.get(".unread-count").text()).toBe("4");
  });

  // Owner, 12 October 2026: nothing on a phone is shortened. The line used to
  // say "Новых:" where its box was narrower than the line; it says
  // "Непрочитанных:" at every width and wraps between its two parts, the
  // number with its word, as every caption line of the site does.
  it("keeps its word at every width and wraps between its parts", () => {
    const { wrapper } = render(1000);
    const line = wrapper.get(".unread-line .caption-line");

    expect(
      line
        .findAll(":scope > .caption-line__part")
        .map((part) => part.text().replace(/\s+/g, " ")),
    ).toEqual(["Непрочитанных: 999+", "Отметить прочитанными"]);
    expect(line.find(".caption-line__bar").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Новых");
    const css = componentCss(resolve(HERE, "UnreadLine.vue"));
    expect(css).not.toMatch(/@container|nowrap/);
  });

  it.each([
    [999, "999"],
    [1000, "999+"],
    [12345, "999+"],
  ])("writes %i as %s", (count, shown) => {
    const { wrapper } = render(count);

    expect(wrapper.get(".unread-count").text()).toBe(shown);
  });

  it("is gone once everything is marked, and says so", async () => {
    const { wrapper, markSpy } = render(4);

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    expect(markSpy).toHaveBeenCalledTimes(1);
    expect(wrapper.find(".unread-line").exists()).toBe(false);
    expect(wrapper.get("[role=status]").text()).toBe(
      "Все уведомления прочитаны",
    );
  });

  it("puts the focus on the heading it stood under", async () => {
    const { wrapper } = render(4);
    const link = wrapper.get(".unread-mark").element as HTMLButtonElement;
    link.focus();

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    const heading = wrapper.get("h1").element;
    expect(document.activeElement).toBe(heading);
    // Reachable from script, not added to the Tab order.
    expect(heading.getAttribute("tabindex")).toBe("-1");
  });

  it("marks once however often it is pressed while it waits", async () => {
    let answer: (error: GeneralError | null) => void = () => {};
    const { wrapper, markSpy } = render(
      4,
      () =>
        new Promise((resolve) => {
          answer = resolve;
        }),
    );

    await wrapper.get(".unread-mark").trigger("click");
    await wrapper.get(".unread-mark").trigger("click");
    expect(wrapper.get(".unread-mark").attributes("aria-busy")).toBe("true");
    answer(null);
    await flushPromises();

    expect(markSpy).toHaveBeenCalledTimes(1);
  });

  it("says a refusal under itself and stays for another try", async () => {
    const { wrapper } = render(4, async () => refused(404));

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    expect(lineText(wrapper)).toBe("Непрочитанных: 4 | Отметить прочитанными");
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось отметить уведомления",
    );
    expect(wrapper.get("[role=status]").text()).toBe("");
    expect(document.activeElement).not.toBe(wrapper.get("h1").element);
  });

  it("says the server's own sentence where it gave one", async () => {
    const { wrapper } = render(4, async () =>
      refused(409, "Уведомления уже отмечены"),
    );

    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    expect(wrapper.get(".action-result").text()).toBe(
      "Уведомления уже отмечены",
    );
  });

  it("takes back what it said when something new arrives", async () => {
    const { wrapper, shown } = render(4);
    await wrapper.get(".unread-mark").trigger("click");
    await flushPromises();

    shown.value = 1;
    await flushPromises();

    expect(lineText(wrapper)).toBe("Непрочитанных: 1 | Отметить прочитанными");
    expect(wrapper.get("[role=status]").text()).toBe("");
  });
});
