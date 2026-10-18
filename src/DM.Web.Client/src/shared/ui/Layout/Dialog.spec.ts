/**
 * @vitest-environment jsdom
 */

/**
 * The window of the site as it behaves, not as it looks: the look is read off
 * the stylesheets in dialogSurface.spec.ts.
 *
 * What the owner approved on 22 September 2026 is behaviour, and each piece of
 * it had a history of going wrong in one window and not the next. A refusal
 * fell as a plate on a page whose window had already closed. A second press
 * sent a second request. Escape dropped a typed reason without a word. The
 * caret started on the button that deletes. So each rule is asked here of the
 * window itself, through the one form the windows of the site hold.
 *
 * The library tier underneath is reduced to a box that carries the class it is
 * handed and remembers what it was asked to do: the window decides every
 * closing itself, and that is the half under test.
 */
import { afterEach, describe, it, expect, vi } from "vitest";
import {
  defineComponent,
  h,
  nextTick,
  onBeforeUnmount,
  onMounted,
  ref,
  type Component,
} from "vue";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { Schema } from "@tiptap/pm/model";
import { EditorState } from "@tiptap/pm/state";
import { EditorView } from "@tiptap/pm/view";
import Dialog from "./Dialog.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { setScrollContainer } from "@/shared/lib/scroll";
import type { GeneralError } from "@/shared/api/models/common";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";
import {
  closeOpenWindows,
  openWindowCount,
} from "@/shared/lib/composables/useOpenWindows";

const library = vi.hoisted(() => ({
  props: {} as Record<string, unknown>,
  /** Takes the content away when closed, as the library does. Off by default. */
  honoursModel: false,
}));

vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    name: "VueFinalModal",
    props: {
      contentClass: { type: String, default: "" },
      focusTrap: { type: Object, default: undefined },
      clickToClose: { type: Boolean, default: true },
      escToClose: { type: Boolean, default: true },
      modelValue: { type: Boolean, default: false },
    },
    setup:
      (props, { slots }) =>
      () => {
        library.props = { ...props };
        if (library.honoursModel && !props.modelValue) return null;
        return h(
          "div",
          { class: ["vfm-content", props.contentClass] },
          slots.default?.(),
        );
      },
  }),
}));

/** A promise the test settles when it wants the answer to arrive. */
function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((yes, no) => {
    resolve = yes;
    reject = no;
  });
  return { promise, resolve, reject };
}

const problem = (
  status: number,
  title = "",
  extra: Partial<GeneralError> & { errors?: Record<string, string[]> } = {},
) => ({ type: "", title, status, traceId: "t", ...extra });

/** The window as a moderation form writes it: one field, the row of two. */
const open = (
  props: InstanceType<typeof Dialog>["$props"] = {},
  body: () => ReturnType<typeof h>[] = () => [
    h(FormField, { label: "Причина", field: "reason" }, () => h("textarea")),
  ],
) =>
  mount(Dialog, {
    attachTo: document.body,
    props,
    slots: {
      default: () =>
        h(Form, { action: "Сохранить", cancel: "Отмена", valid: true }, () =>
          body(),
        ),
    },
  });

type Wrapper = ReturnType<typeof open>;

const main = (w: Wrapper) => w.get(".controls button.primary");
const cancel = (w: Wrapper) => w.findAll(".controls button")[1];
const cross = (w: Wrapper) => w.get(".dialog > .dialog-close");
const frame = (w: Wrapper) => w.get(".vfm-content");
const line = (w: Wrapper) => w.find(".form-refusal");
const question = (w: Wrapper) => w.find(".dialog-backdrop");

const pressEscape = (w: Wrapper) =>
  frame(w).trigger("keydown", { key: "Escape" });

const schema = new Schema({
  nodes: {
    doc: { content: "paragraph+" },
    paragraph: { content: "text*", toDOM: () => ["p", 0] },
    text: {},
  },
});

/**
 * The editor of the warning and ban windows, as far as a key pressed in it
 * goes: ProseMirror itself, not a stand-in, since what it does with Escape is
 * the thing under test.
 */
const EditorField = defineComponent({
  setup() {
    const host = ref<HTMLElement | null>(null);
    let view: EditorView | null = null;
    onMounted(() => {
      view = new EditorView(host.value, {
        state: EditorState.create({ schema }),
      });
    });
    onBeforeUnmount(() => view?.destroy());
    return () => h("div", { ref: host });
  },
});

const inEditor = () => [
  h(FormField, { label: "Причина", field: "reason" }, () => h(EditorField)),
];

/** Escape pressed with the caret in the editor. */
const escapeInEditor = (w: Wrapper): KeyboardEvent => {
  const press = new KeyboardEvent("keydown", {
    key: "Escape",
    keyCode: 27,
    bubbles: true,
    cancelable: true,
  });
  w.get(".ProseMirror").element.dispatchEvent(press);
  return press;
};

const clickBeside = async (w: Wrapper) => {
  await frame(w).trigger("mousedown");
  await frame(w).trigger("mouseup");
};

const closings = (w: Wrapper) => ({
  close: w.emitted("close")?.length ?? 0,
  shut: w.emitted("update:modelValue") ?? [],
});

// A window left open by a test would keep holding the page for the next one.
enableAutoUnmount(afterEach);

afterEach(() => {
  document.body.innerHTML = "";
  setScrollContainer(null);
});

/** Out of reach: the attribute in a browser, the property where jsdom has no reflection. */
const inert = (w: Wrapper) => {
  const card = w.get(".dialog").element as HTMLElement;
  return card.hasAttribute("inert") || card.inert === true;
};

describe("a window that runs an action", () => {
  it("sends one request however often it is pressed, and waits for it shut", async () => {
    const answer = deferred<DialogActionResult>();
    const action = vi.fn(() => answer.promise);
    const w = open({ action });

    await w.get("form").trigger("submit");
    await w.get("form").trigger("submit");
    expect(action).toHaveBeenCalledTimes(1);

    // The main button hatched and busy, "Отмена" out, the cross dim.
    expect(main(w).attributes("disabled")).toBeDefined();
    expect(main(w).attributes("aria-busy")).toBe("true");
    expect(cancel(w).attributes("disabled")).toBeDefined();
    expect(cross(w).attributes("aria-disabled")).toBe("true");

    // No way out answers until the request does.
    await pressEscape(w);
    await cross(w).trigger("click");
    await clickBeside(w);
    expect(closings(w)).toEqual({ close: 0, shut: [] });

    answer.resolve({ error: null });
    await flushPromises();

    expect(w.emitted("done")).toEqual([[{ error: null }]]);
    expect(closings(w)).toEqual({ close: 0, shut: [[false]] });
  });

  it("prints a refusal over the buttons and stays open", async () => {
    const answer = deferred<DialogActionResult>();
    const later = deferred<DialogActionResult>();
    const action = vi
      .fn()
      .mockReturnValueOnce(answer.promise)
      .mockReturnValueOnce(later.promise);
    const w = open({ action });

    await w.get("form").trigger("submit");
    answer.resolve({ error: problem(409, "Тег с таким именем уже есть") });
    await flushPromises();

    expect(line(w).text()).toBe("Тег с таким именем уже есть");
    expect(line(w).attributes("role")).toBe("alert");
    expect(line(w).element.nextElementSibling?.classList).toContain("controls");
    expect(closings(w)).toEqual({ close: 0, shut: [] });
    expect(w.emitted("done")).toBeUndefined();

    // The window is live again, and the next press clears the old refusal.
    expect(main(w).attributes("disabled")).toBeUndefined();
    expect(cross(w).attributes("aria-disabled")).toBeUndefined();
    await w.get("form").trigger("submit");
    expect(line(w).exists()).toBe(false);
  });

  it("says the window's own sentence when the server named nothing", async () => {
    const w = open({
      action: async () => ({ error: problem(404) }),
      failure: "Не удалось изменить баллы",
    });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(line(w).text()).toBe("Не удалось изменить баллы");
  });

  it("puts a field's refusal under that field, and a field nobody shows in the line", async () => {
    const w = open({
      action: async () => ({
        error: problem(400, "Validation failed", {
          errors: { Reason: ["Long"], Points: ["Invalid"] },
        }),
      }),
    });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(w.get(".form-field-error").text()).toBe("Слишком длинное значение");
    expect(w.get("textarea").attributes("aria-invalid")).toBe("true");
    expect(line(w).text()).toBe("Некорректное значение");
  });

  it("leaves the line empty when every refused field is on screen", async () => {
    const w = open({
      action: async () => ({
        error: problem(400, "Validation failed", {
          errors: { Reason: ["Empty"] },
        }),
      }),
    });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(w.get(".form-field-error").text()).toBe("Обязательное поле");
    expect(line(w).exists()).toBe(false);
  });

  it("counts a rate limit down in the line and holds the button until zero", async () => {
    vi.useFakeTimers();
    try {
      const w = open({
        action: async () => ({
          error: problem(429, "", { retryAfterSeconds: 2 }),
        }),
      });

      await w.get("form").trigger("submit");
      await flushPromises();

      expect(line(w).text()).toBe(
        "Слишком много попыток, попробуйте через 2 секунды",
      );
      expect(main(w).attributes("disabled")).toBeDefined();
      expect(main(w).attributes("aria-busy")).toBeUndefined();

      vi.advanceTimersByTime(2000);
      await nextTick();

      expect(line(w).exists()).toBe(false);
      expect(main(w).attributes("disabled")).toBeUndefined();
    } finally {
      vi.useRealTimers();
    }
  });

  it("closes without a word when its own request finds the session gone", async () => {
    const w = open({
      action: async () => ({ error: problem(401) }),
      dirty: true,
    });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(question(w).exists()).toBe(false);
    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });

  it("leaves a request that never arrived to the interceptor, and lets go", async () => {
    const w = open({ action: async () => ({ error: problem(0) }) });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(line(w).exists()).toBe(false);
    expect(main(w).attributes("disabled")).toBeUndefined();
    expect(closings(w)).toEqual({ close: 0, shut: [] });
  });

  it("counts a throw as a failure, and lets go", async () => {
    const w = open({
      action: async () => {
        throw new Error("the store broke after the answer");
      },
    });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(line(w).text()).toBe(
      "Не удалось выполнить запрос, попробуйте позже",
    );
    expect(main(w).attributes("aria-busy")).toBeUndefined();
    expect(cross(w).attributes("aria-disabled")).toBeUndefined();
  });

  it("closes after an action that asks no server", async () => {
    const w = open({ action: () => undefined });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(w.emitted("done")).toEqual([[undefined]]);
    expect(closings(w)).toEqual({ close: 0, shut: [[false]] });
  });

  it("keeps a link in the card from leading away while it waits", async () => {
    const answer = deferred<DialogActionResult>();
    // What the router's link does with a click: it leads away unless the click
    // arrives already declined. Declined here after the check, because jsdom
    // cannot navigate.
    let leadsAway = false;
    const w = open({ action: () => answer.promise }, () => [
      h(
        "a",
        {
          href: "/profile/someone",
          onClick: (e: MouseEvent) => {
            leadsAway = !e.defaultPrevented;
            e.preventDefault();
          },
        },
        "someone",
      ),
    ]);
    const follow = () => {
      leadsAway = false;
      w.get("a").element.dispatchEvent(
        new MouseEvent("click", { bubbles: true, cancelable: true }),
      );
      return leadsAway;
    };

    expect(follow()).toBe(true);
    await w.get("form").trigger("submit");
    expect(follow()).toBe(false);

    answer.resolve({ error: problem(409, "Нельзя") });
    await flushPromises();
    expect(follow()).toBe(true);
  });

  it("parks the caret on the cross while the pressed button is shut, and brings it back", async () => {
    const answer = deferred<DialogActionResult>();
    const w = open({ action: () => answer.promise });

    (main(w).element as HTMLButtonElement).focus();
    await main(w).trigger("click");
    expect(document.activeElement).toBe(cross(w).element);

    answer.resolve({ error: problem(409, "Нельзя") });
    await flushPromises();
    await nextTick();
    expect(document.activeElement).toBe(main(w).element);
  });
});

describe("typed text in a window", () => {
  it("opens again without the question it was closed by", async () => {
    // "Закрыть" closes the window, and the window takes the question away with
    // it before the question can report that it closed.
    library.honoursModel = true;
    try {
      const Host = defineComponent({
        setup() {
          const open = ref(true);
          return { open };
        },
        render() {
          return h(
            Dialog,
            {
              modelValue: this.open,
              "onUpdate:modelValue": (v: boolean) => (this.open = v),
              dirty: true,
            },
            () => h("textarea"),
          );
        },
      });
      const w = mount(Host, { attachTo: document.body });

      await w.get(".dialog > .dialog-close").trigger("click");
      await w.get(".dialog-backdrop .dialog-btn-submit").trigger("click");
      await flushPromises();
      expect(w.find(".dialog").exists()).toBe(false);

      (w.vm as unknown as { open: boolean }).open = true;
      await nextTick();
      expect(w.find(".dialog").exists()).toBe(true);
      expect(w.find(".dialog-backdrop").exists()).toBe(false);
    } finally {
      library.honoursModel = false;
    }
  });

  it('is asked about before Escape, the cross or "Отмена" let it go', async () => {
    const w = open({ dirty: true, action: () => new Promise(() => {}) });

    await pressEscape(w);
    expect(question(w).exists()).toBe(true);
    expect(question(w).get(".dialog-title").text()).toBe(
      "Закрыть без сохранения?",
    );
    expect(question(w).get(".dialog-message").text()).toBe(
      "Набранный текст пропадет",
    );
    expect(
      question(w)
        .findAll(".dialog-actions button")
        .map((one) => one.text()),
    ).toEqual(["Закрыть", "Отмена"]);
    // The window under the question is out of reach while it stands.
    expect(inert(w)).toBe(true);
    expect(closings(w).close).toBe(0);

    // "Отмена" of the question returns to the text.
    await question(w).get(".dialog-btn-cancel").trigger("click");
    expect(question(w).exists()).toBe(false);
    expect(inert(w)).toBe(false);

    await cross(w).trigger("click");
    expect(question(w).exists()).toBe(true);
    await question(w).get(".dialog-btn-cancel").trigger("click");

    await cancel(w).trigger("click");
    expect(question(w).exists()).toBe(true);
    expect(closings(w)).toEqual({ close: 0, shut: [] });

    // "Закрыть" lets it go.
    await question(w).get(".dialog-btn-submit").trigger("click");
    await flushPromises();
    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });

  it("keeps a click beside the card from closing it, and does not ask either", async () => {
    const w = open({ dirty: true });

    await clickBeside(w);

    expect(question(w).exists()).toBe(false);
    expect(closings(w)).toEqual({ close: 0, shut: [] });
  });

  it("is asked about before Escape from the editor lets it go", async () => {
    const w = open({ dirty: true }, inEditor);

    escapeInEditor(w);
    await nextTick();

    expect(question(w).exists()).toBe(true);
    expect(closings(w).close).toBe(0);
  });

  it("leaves Escape on the question to the question", async () => {
    const w = open({ dirty: true });

    await pressEscape(w);
    await question(w).trigger("keydown", { key: "Escape" });

    // The question is gone, and the press that closed it neither asked again
    // nor closed the window under it.
    expect(question(w).exists()).toBe(false);
    expect(inert(w)).toBe(false);
    expect(closings(w)).toEqual({ close: 0, shut: [] });
  });

  it("names what is lost in the caller's words", async () => {
    const w = open({ dirty: true, discardText: "Набранная причина пропадет" });

    await pressEscape(w);

    expect(question(w).get(".dialog-message").text()).toBe(
      "Набранная причина пропадет",
    );
  });

  it("closes after success without asking", async () => {
    const w = open({ dirty: true, action: async () => ({ error: null }) });

    await w.get("form").trigger("submit");
    await flushPromises();

    expect(question(w).exists()).toBe(false);
    expect(closings(w)).toEqual({ close: 0, shut: [[false]] });
  });
});

describe("a window with nothing typed", () => {
  it("closes on a click beside the card, and not on a selection dragged out of it", async () => {
    const w = open();

    // Pressed in the card, let go beside it.
    await w.get("textarea").trigger("mousedown");
    await frame(w).trigger("mouseup");
    expect(closings(w).close).toBe(0);

    await clickBeside(w);
    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });

  it("closes on Escape from the editor, which prevents the default of it", () => {
    const w = open({}, inEditor);

    const press = escapeInEditor(w);

    // What the editor does with every Escape: it is not a control closing
    // something of its own, and the window is not to read it as one.
    expect(press.defaultPrevented).toBe(true);
    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });
});

describe("the caret's first place", () => {
  const initialFocus = () =>
    (
      library.props.focusTrap as { initialFocus: () => HTMLElement }
    ).initialFocus();

  it("is the first empty field, and Escape stays the window's", () => {
    const w = open({}, () => [
      h(FormField, { label: "Имя" }, () => h("input", { value: "Тег" })),
      h(FormField, { label: "Описание" }, () => h("textarea")),
    ]);

    expect(initialFocus()).toBe(w.get("textarea").element);
    expect(library.props.focusTrap).toMatchObject({
      escapeDeactivates: false,
      allowOutsideClick: true,
    });
  });

  it("is the first field when every field is filled", () => {
    const w = open({}, () => [
      h(FormField, { label: "Имя" }, () => h("input", { value: "Тег" })),
    ]);

    expect(initialFocus()).toBe(w.get("input").element);
  });

  it("is where the opener put it, if it put it inside", () => {
    const w = open();
    (w.get(".controls button.primary").element as HTMLElement).focus();

    expect(initialFocus()).toBe(w.get(".controls button.primary").element);
  });

  it("is the cross in a window with no field", () => {
    const w = open({}, () => [h("p", "Текст")]);

    expect(initialFocus()).toBe(cross(w).element);
  });
});

describe("the body of a window", () => {
  it("is told while the request is in flight", async () => {
    const answer = deferred<DialogActionResult>();
    const seen: boolean[] = [];
    const w = mount(Dialog, {
      props: { action: () => answer.promise },
      slots: {
        default: (scope: { busy: boolean }) => {
          seen.push(scope.busy);
          return h(Form, { action: "Выдать", valid: true }, () =>
            h("textarea", { disabled: scope.busy }),
          );
        },
      },
    });

    await w.get("form").trigger("submit");
    expect(w.get("textarea").attributes("disabled")).toBeDefined();

    answer.resolve({ error: problem(409, "Нельзя") });
    await flushPromises();
    expect(w.get("textarea").attributes("disabled")).toBeUndefined();
    expect(seen).toContain(true);
  });
});

describe("a window with no action", () => {
  it("leaves the form its own events", async () => {
    const onSubmit = vi.fn();
    const onCancel = vi.fn();
    const w = mount(Dialog, {
      slots: {
        default: () =>
          h(
            Form as Component,
            { action: "Сохранить", cancel: "Отмена", onSubmit, onCancel },
            () => h("input"),
          ),
      },
    });

    await w.get("form").trigger("submit");
    await w.findAll(".controls button")[1].trigger("click");

    expect(onSubmit).toHaveBeenCalledTimes(1);
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(w.emitted("update:modelValue")).toBeUndefined();
  });
});

describe("the guest card", () => {
  it("keeps the library's own closing and its own caret", async () => {
    const w = mount(Dialog, {
      props: { auto: true },
      slots: { default: "<div class='auth-surface'></div>" },
    });

    expect(library.props).toMatchObject({
      clickToClose: true,
      escToClose: true,
      focusTrap: undefined,
    });
    await w.get(".vfm-content").trigger("keydown", { key: "Escape" });
    expect(w.emitted("close")).toBeUndefined();
  });
});

describe("the page under a window", () => {
  it("is held still, with the place of its scrollbar kept, while the window is open", async () => {
    const page = document.createElement("div");
    page.style.overflow = "";
    setScrollContainer(page);
    const w = open();
    const tier = w.findComponent({ name: "VueFinalModal" });

    tier.vm.$emit("beforeOpen");
    expect(page.style.overflow).toBe("hidden");
    expect(page.style.getPropertyValue("scrollbar-gutter")).toBe("stable");

    tier.vm.$emit("closed");
    expect(page.style.overflow).toBe("");
    expect(page.style.getPropertyValue("scrollbar-gutter")).toBe("");
  });

  it("is let go when the window leaves with its screen", () => {
    const page = document.createElement("div");
    setScrollContainer(page);
    const w = open();

    w.findComponent({ name: "VueFinalModal" }).vm.$emit("beforeOpen");
    w.unmount();

    expect(page.style.overflow).toBe("");
  });
});

/**
 * The owner's word: the windows close when the site learns the session is
 * over (sessionExpiry.ts calls closeOpenWindows). What typed text there is
 * cannot be sent any more, so nothing is asked. The guest card stays: it is
 * how the reader signs in again, and the end of the session is what opens it.
 */
describe("the end of the session", () => {
  it("closes an open window without asking about typed text", async () => {
    const w = open({ modelValue: true, dirty: true });

    closeOpenWindows();
    await nextTick();

    expect(question(w).exists()).toBe(false);
    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });

  it("leaves a closed window alone", () => {
    const w = open({ modelValue: false });

    closeOpenWindows();

    expect(closings(w)).toEqual({ close: 0, shut: [] });
  });

  it("keeps the guest card open", () => {
    const w = mount(Dialog, {
      props: { auto: true, modelValue: true },
      slots: { default: "<div class='auth-surface'></div>" },
    });

    closeOpenWindows();

    expect(w.emitted("close")).toBeUndefined();
    expect(w.emitted("update:modelValue")).toBeUndefined();
  });

  // Its own request meets the same refusal and closes it (onExpired); asked
  // here too, the window would say "close" twice.
  it("leaves a window waiting for its request to that request", async () => {
    const answer = deferred<{ error: ReturnType<typeof problem> }>();
    const w = open({ modelValue: true, action: () => answer.promise });

    await w.get("form").trigger("submit");
    closeOpenWindows();
    answer.resolve({ error: problem(401) });
    await flushPromises();

    expect(closings(w)).toEqual({ close: 1, shut: [[false]] });
  });

  it("stops counting a window that left with its screen", () => {
    const before = openWindowCount();
    const w = open({ modelValue: true });
    expect(openWindowCount()).toBe(before + 1);

    w.unmount();

    expect(openWindowCount()).toBe(before);
  });
});
