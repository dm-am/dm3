/**
 * @vitest-environment jsdom
 */

/**
 * The window that asks before something cannot be taken back.
 *
 * What it has to get right is not markup, it is what a reader sees: a question
 * with the verb of the button in it, one sentence saying what is lost, the
 * action offered before the way out, and no red anywhere. The red is the part
 * with a history — the window used to paint the confirm button in the site's
 * destructive fill, so the reader recognised the colour and stopped reading
 * the sentence. The danger now lives in the words, which is the one place it
 * cannot be skimmed past. Owner's call, 21 September 2026.
 *
 * Half of that decision is a rule and not a node: the card's measure, its
 * gutter, the absence of a frame and of a shadow, the height of the row. jsdom
 * applies no scoped styles and vitest never compiles a <style> block, so an
 * assertion about any of them through a mount would be an assertion about
 * markup, and markup is not where they live. Those are read off the compiled
 * stylesheet instead.
 */
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
  ruleMap,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { setScrollContainer } from "@/shared/lib/scroll";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";

const HERE = dirname(fileURLToPath(import.meta.url));

// Stub Teleport to render content in place (not to body).
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

/** The site's button, which draws both halves of the row. */
const buttonCss = (): string =>
  componentCss(resolve(HERE, "../Button/Button.vue"));

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

    // No node wears the destructive modifier: the class is what used to turn
    // the confirm button into the site's red control.
    const red = wrapper
      .findAll("*")
      .filter((one) => one.classes().some((name) => /danger/.test(name)));
    expect(red).toEqual([]);

    // And nothing in the stylesheet can bring the colour back by another
    // route: the one rule painted in a token of the red family is the line of
    // a refusal, and the destructive mixin is not called. The compiled sheet is
    // asked for the tokens and the source for the mixin, because a mixin
    // leaves its own name nowhere in the output.
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

  it("stops the action while the request is in flight", async () => {
    const wrapper = open({ loading: true });
    const action = wrapper.findAll(".dialog-actions button")[0];

    expect(action.attributes("disabled")).toBeDefined();
    expect(action.attributes("aria-busy")).toBe("true");

    await action.trigger("click");
    expect(wrapper.emitted("confirm")).toBeUndefined();
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

/** A promise the test settles when it wants the answer to arrive. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((yes) => {
    resolve = yes;
  });
  return { promise, resolve };
}

const problem = (status: number, title = "", seconds?: number) => ({
  type: "",
  title,
  status,
  traceId: "t",
  ...(seconds ? { retryAfterSeconds: seconds } : {}),
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
        action: async () => ({ error: problem(429, "", 1) }),
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
  it("has one measure, and it is the guest card's narrow one", () => {
    expect(ruleMap(windowCss()).get(".dialog-container")?.width).toBe("352px");
  });

  it("keeps the guest card's gutter, fill and radius", () => {
    const card = ruleMap(windowCss()).get(".dialog-container");

    expect(card?.padding).toBe("24px");
    expect(card?.["background-color"]).toBe("var(--bg-page)");
    expect(card?.["border-radius"]).toBe("8px");
  });

  it("stands over the page with neither a frame nor a shadow", () => {
    const css = windowCss();

    expect(ruleMap(css).get(".dialog-container")?.border).toBeUndefined();

    // The one shadow left in the sheet is the focus ring of the cross, which
    // follows the button's own rounded edge because an outline in the corner
    // of the card is clipped by the card.
    expect(
      styleRules(css)
        .filter(({ declarations }) => declarations["box-shadow"])
        .map(({ selector }) => selector),
    ).toEqual([".dialog-close:focus-visible"]);
  });

  it("centres the question and the sentence under it", () => {
    expect(ruleMap(windowCss()).get(".dialog-container")?.["text-align"]).toBe(
      "center",
    );
  });

  it("scrolls a card taller than the screen whole, from its top", () => {
    const sheet = ruleMap(windowCss());

    expect(sheet.get(".dialog-backdrop")).toMatchObject({
      "overflow-y": "auto",
      padding: "16px",
      display: "flex",
    });
    // Centring by align-items would cut a tall card at both ends.
    expect(sheet.get(".dialog-backdrop")?.["align-items"]).toBeUndefined();
    expect(sheet.get(".dialog-container")?.margin).toBe("auto");
  });

  it("dims the page behind it without blurring it", () => {
    const backdrop = ruleMap(windowCss()).get(".dialog-backdrop");

    expect(backdrop?.["background-color"]).toBe("var(--overlay-bg)");
    // A blur would take away the thing the question is about while the reader
    // decides. The guest dialogs have none either.
    expect(backdrop?.["backdrop-filter"]).toBeUndefined();
  });

  it("arrives by fading and does nothing else", () => {
    const rules = styleRules(windowCss()).filter(({ selector }) =>
      selector.includes("dialog-enter"),
    );

    expect(rules.length).toBeGreaterThan(0);
    for (const { selector, declarations } of rules) {
      expect(declarations.transform, selector).toBeUndefined();
    }
  });

  it("stands the two halves of the row one step apart", () => {
    const sheet = ruleMap(windowCss());

    expect(sheet.get(".dialog-actions")?.gap).toBe("8px");
    // A zero basis: the captions differ in length, and a basis taken from the
    // content would leave the halves unequal.
    expect(sheet.get(".dialog-actions > *")?.flex).toBe("1 1 0");
  });

  it("stands the row at the window's control height and not the page's", () => {
    // The row is the site's own button twice over, 38 high on a page; in a
    // window it is 52, the height of the window with a form. Which of the two
    // rules wins is asked in dialogSurface.spec.ts, from the scoped sheets.
    expect(ruleMap(buttonCss()).get("button")?.height).toBe("38px");
    expect(ruleMap(windowCss()).get(".dialog-actions > *")?.height).toBe(
      "52px",
    );
  });

  it("asks the question in the site's heading style, centred", () => {
    const sheet = ruleMap(windowCss());
    const title = sheet.get(".dialog-title");

    expect(title?.["text-transform"]).toBe("uppercase");
    expect(title?.color).toBe("var(--heading)");
    expect(title?.["font-weight"]).toBe("700");
    expect(title?.["font-size"]).toBe("16px");
    expect(title?.["letter-spacing"]).toBe("0.5px");
    // Centred with the sentence under it, the reserve for the cross taken on
    // both sides so the centring stays honest.
    expect(sheet.get(".dialog-container")?.["text-align"]).toBe("center");
    expect(title?.padding).toBe("0 24px");
  });
});
