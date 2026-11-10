/**
 * @vitest-environment jsdom
 */

// No red anywhere in the window: the danger lives in the words, the one place it cannot be skimmed past.
import { afterEach, describe, it, expect, vi } from "vitest";
import { nextTick } from "vue";
import {
  config,
  enableAutoUnmount,
  flushPromises,
  mount,
} from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ConfirmDialog from "./ConfirmDialog.vue";
import {
  componentCss,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { setScrollContainer } from "@/shared/lib/scroll";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import { deferred } from "@/shared/lib/testing/tasks";
import { problem } from "@/shared/lib/testing/problem";

const HERE = dirname(fileURLToPath(import.meta.url));

config.global.stubs = {
  Teleport: true,
};

// A window left open by a test would keep holding the page for the next one.
enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

const open = (props: Record<string, unknown> = {}) =>
  mount(ConfirmDialog, {
    attachTo: document.body,
    props: {
      show: true,
      title: "Удалить файл?",
      message: 'Файл "portrait.png" исчезнет из постов, куда он вставлен.',
      confirmLabel: "Удалить",
      ...props,
    },
  });

/** The captions of the action row, left to right. */
const captions = (wrapper: ReturnType<typeof open>): string[] =>
  wrapper.findAll(".dialog-actions button").map((one) => one.text());

/** The window's own stylesheet, as the browser gets it. */
const windowCss = (): string =>
  componentCss(resolve(HERE, "ConfirmDialog.vue"));

describe("the confirmation window", () => {
  it("announces itself as a decision to take", () => {
    const container = open().get(".dialog-container");

    // "alertdialog" and not "dialog": a screen reader announces this one
    // together with the text inside it, which is the whole of what the window
    // has to say.
    expect(container.attributes("role")).toBe("alertdialog");
    expect(container.attributes("aria-modal")).toBe("true");
    expect(container.attributes("aria-label")).toBe("Удалить файл?");
  });

  it("asks the question and names what the answer costs", () => {
    const wrapper = open();

    expect(wrapper.get(".dialog-title").text()).toBe("Удалить файл?");
    expect(wrapper.get(".dialog-message").text()).toBe(
      'Файл "portrait.png" исчезнет из постов, куда он вставлен.',
    );
  });

  it("leaves no empty strip where a window carries no sentence", () => {
    const wrapper = open({ message: "" });

    expect(wrapper.find(".dialog-message").exists()).toBe(false);
  });

  it("offers the action first and the way out after it", () => {
    expect(captions(open())).toEqual(["Удалить", "Отмена"]);
  });

  it("gives the action the site's primary fill and nothing else a fill", () => {
    const wrapper = open();
    const buttons = wrapper.findAll(".dialog-actions button");

    expect(buttons[0].classes()).toContain("primary");
    expect(buttons[1].classes()).not.toContain("primary");
  });

  it("paints nothing in the window red but a refusal", () => {
    const wrapper = open();

    const red = wrapper
      .findAll("*")
      .filter((one) => one.classes().some((name) => /danger/.test(name)));
    expect(red).toEqual([]);

    // The compiled sheet is asked for the red tokens, the source for the mixin: a mixin leaves no name in the output.
    const painted = styleRules(windowCss())
      .filter(({ declarations }) =>
        Object.values(declarations).some((value) =>
          /var\(--[\w-]*red[\w-]*\)/.test(value),
        ),
      )
      .map(({ selector }) => selector);
    expect(painted).toEqual([".dialog-refusal"]);
    expect(sassBlocks(resolve(HERE, "ConfirmDialog.vue"))).not.toContain(
      "button-danger",
    );
  });

  it("starts the caret on the way out, not on the action", async () => {
    const wrapper = open();
    await nextTick();

    expect(document.activeElement).toBe(
      wrapper.get(".dialog-btn-cancel").element,
    );
  });

  it("confirms nothing on Enter from the card itself", async () => {
    const wrapper = open();

    await wrapper.get(".dialog-container").trigger("keydown", { key: "Enter" });

    expect(wrapper.emitted("confirm")).toBeUndefined();
  });

  it("stays open under a selection dragged out of the card", async () => {
    const wrapper = open();

    await wrapper.get(".dialog-title").trigger("mousedown");
    await wrapper.get(".dialog-backdrop").trigger("click");

    expect(wrapper.emitted("cancel")).toBeUndefined();
  });

  it("holds the page still, with the place of its scrollbar kept, while it is open", async () => {
    const page = document.createElement("div");
    setScrollContainer(page);
    const wrapper = open({ show: false });

    await wrapper.setProps({ show: true });
    expect(page.style.overflow).toBe("hidden");
    expect(page.style.getPropertyValue("scrollbar-gutter")).toBe("stable");

    await wrapper.setProps({ show: false });
    expect(page.style.overflow).toBe("");
    expect(page.style.getPropertyValue("scrollbar-gutter")).toBe("");
  });

  it("answers the action and the way out", async () => {
    const wrapper = open();
    const buttons = wrapper.findAll(".dialog-actions button");

    await buttons[0].trigger("click");
    expect(wrapper.emitted("confirm")).toHaveLength(1);

    await buttons[1].trigger("click");
    expect(wrapper.emitted("cancel")).toHaveLength(1);
    expect(wrapper.emitted("update:show")).toEqual([[false]]);
  });

  it("closes with a cross that carries no glyph of its own", async () => {
    const wrapper = open();
    const cross = wrapper.get("button.dialog-close");

    expect(cross.attributes("aria-label")).toBe("Закрыть");
    expect(cross.element.innerHTML).toBe("");

    await cross.trigger("click");
    expect(wrapper.emitted("cancel")).toHaveLength(1);
  });

  it("closes on Escape and on a click beside the card", async () => {
    const wrapper = open();

    await wrapper.get(".dialog-backdrop").trigger("keydown", { key: "Escape" });
    expect(wrapper.emitted("cancel")).toHaveLength(1);

    const backdrop = wrapper.get(".dialog-backdrop");
    await backdrop.trigger("click");
    expect(wrapper.emitted("cancel")).toHaveLength(2);
  });
});

describe("a confirmation that runs its action", () => {
  type Wrapper = ReturnType<typeof open>;
  const confirm = (wrapper: Wrapper) => wrapper.get(".dialog-btn-submit");
  const way = (wrapper: Wrapper) => wrapper.get(".dialog-btn-cancel");
  const line = (wrapper: Wrapper) => wrapper.find(".dialog-refusal");

  it("sends one request, waits for it shut, and closes on success", async () => {
    const answer = deferred<DialogActionResult>();
    const action = vi.fn(() => answer.promise);
    const wrapper = open({ action });

    await confirm(wrapper).trigger("click");
    await confirm(wrapper).trigger("click");
    expect(action).toHaveBeenCalledTimes(1);

    expect(confirm(wrapper).attributes("aria-busy")).toBe("true");
    expect(confirm(wrapper).attributes("disabled")).toBeDefined();
    expect(way(wrapper).attributes("disabled")).toBeDefined();
    expect(wrapper.get(".dialog-close").attributes("aria-disabled")).toBe(
      "true",
    );

    // Every way out is shut until the answer.
    await wrapper.get(".dialog-close").trigger("click");
    await wrapper.get(".dialog-backdrop").trigger("keydown", { key: "Escape" });
    await wrapper.get(".dialog-backdrop").trigger("click");
    expect(wrapper.emitted("cancel")).toBeUndefined();
    expect(wrapper.emitted("update:show")).toBeUndefined();

    answer.resolve({ error: null });
    await flushPromises();

    expect(wrapper.emitted("done")).toEqual([[{ error: null }]]);
    expect(wrapper.emitted("update:show")).toEqual([[false]]);
    expect(wrapper.emitted("confirm")).toBeUndefined();
  });

  it("says a refusal over the row and stays open", async () => {
    const wrapper = open({
      action: async () => ({
        error: problem(403, "Файл используется в посте"),
      }),
    });

    await confirm(wrapper).trigger("click");
    await flushPromises();

    expect(line(wrapper).text()).toBe("Файл используется в посте");
    expect(line(wrapper).attributes("role")).toBe("alert");
    expect(line(wrapper).element.nextElementSibling?.classList).toContain(
      "dialog-actions",
    );
    expect(wrapper.emitted("update:show")).toBeUndefined();
    expect(confirm(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("says its own sentence when the server named nothing", async () => {
    const wrapper = open({
      action: async () => ({ error: problem(404) }),
      failure: "Не удалось удалить файл",
    });

    await confirm(wrapper).trigger("click");
    await flushPromises();

    expect(line(wrapper).text()).toBe("Не удалось удалить файл");
  });

  it("counts a rate limit down and holds the action until zero", async () => {
    vi.useFakeTimers();
    try {
      const wrapper = open({
        action: async () => ({
          error: problem(429, "", { retryAfterSeconds: 1 }),
        }),
      });

      await confirm(wrapper).trigger("click");
      await flushPromises();

      expect(line(wrapper).text()).toBe(
        "Слишком много попыток, попробуйте через 1 секунду",
      );
      expect(confirm(wrapper).attributes("disabled")).toBeDefined();

      vi.advanceTimersByTime(1000);
      await nextTick();
      expect(line(wrapper).exists()).toBe(false);
      expect(confirm(wrapper).attributes("disabled")).toBeUndefined();
    } finally {
      vi.useRealTimers();
    }
  });

  it("closes without a word when the session is gone", async () => {
    const wrapper = open({ action: async () => ({ error: problem(401) }) });

    await confirm(wrapper).trigger("click");
    await flushPromises();

    expect(line(wrapper).exists()).toBe(false);
    expect(wrapper.emitted("cancel")).toHaveLength(1);
    expect(wrapper.emitted("update:show")).toEqual([[false]]);
  });

  it("parks the caret on the cross while the action is shut, and brings it back", async () => {
    const answer = deferred<DialogActionResult>();
    const wrapper = open({ action: () => answer.promise });
    // The caret has taken its first place on "Отмена" by the time a reader
    // gets to the action.
    await nextTick();

    (confirm(wrapper).element as HTMLButtonElement).focus();
    await confirm(wrapper).trigger("click");
    expect(document.activeElement).toBe(wrapper.get(".dialog-close").element);

    answer.resolve({ error: problem(409, "Нельзя") });
    await flushPromises();
    await nextTick();
    expect(document.activeElement).toBe(confirm(wrapper).element);
  });

  it("forgets the last refusal when it opens again", async () => {
    const wrapper = open({
      action: async () => ({ error: problem(409, "Нельзя") }),
    });

    await confirm(wrapper).trigger("click");
    await flushPromises();
    await wrapper.setProps({ show: false });
    await wrapper.setProps({ show: true });

    expect(line(wrapper).exists()).toBe(false);
  });
});

describe("the card of the confirmation window", () => {
  it("arrives by fading and does nothing else", () => {
    const rules = styleRules(windowCss()).filter(({ selector }) =>
      selector.includes("dialog-enter"),
    );

    expect(rules.length).toBeGreaterThan(0);
    for (const { selector, declarations } of rules) {
      expect(declarations.transform, selector).toBeUndefined();
    }
  });
});
