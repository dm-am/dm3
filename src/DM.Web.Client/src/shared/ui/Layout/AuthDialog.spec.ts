import { afterEach, describe, it, expect, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import AuthDialog from "./AuthDialog.vue";

/** What the library was last told: whether Escape and a click beside the card close. */
const library = vi.hoisted(() => ({ props: {} as Record<string, unknown> }));

vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    name: "VueFinalModal",
    props: {
      clickToClose: { type: Boolean, default: true },
      escToClose: { type: Boolean, default: true },
    },
    setup:
      (props, { slots }) =>
      () => {
        library.props = { ...props };
        return h("div", { class: "vfm-content" }, slots.default?.());
      },
  }),
}));

enableAutoUnmount(afterEach);

/**
 * The cross is the visible way out of a guest dialog, and it is drawn rather
 * than typed.
 *
 * The cross is drawn with bars, not the site's glyph: every typographic cross the
 * site ships is thinner than the marks it stands beside, and how much thinner
 * depends on which font actually loaded, so the same dialog closed with a
 * different weight of cross on two machines. The bars come from the stylesheet,
 * which leaves the button empty: a glyph left inside would stand behind them.
 */
const dialog = () =>
  mount(AuthDialog, {
    global: { stubs: { Dialog: { template: "<div><slot /></div>" } } },
    slots: { default: "<p>тело карточки</p>" },
  });

describe("AuthDialog", () => {
  it("paints the guest card around whatever the form put in it", () => {
    expect(dialog().get(".auth-surface .auth-card").text()).toBe(
      "тело карточки",
    );
  });

  it("closes with a cross that carries no glyph of its own", () => {
    const cross = dialog().get("button.auth-close");

    expect(cross.attributes("aria-label")).toBe("Закрыть");
    expect(cross.attributes("type")).toBe("button");
    expect(cross.element.innerHTML).toBe("");
  });

  it("asks the host to close when the cross is pressed", async () => {
    const wrapper = dialog();

    await wrapper.get("button.auth-close").trigger("click");

    expect(wrapper.emitted("close")).toHaveLength(1);
  });
});

/**
 * While the form's request runs the window is locked, as every site window is
 * (UI_STANDARDS/dialogs.md): useModal unmounts a closed form, and the answer
 * would arrive nowhere.
 */
describe("AuthDialog while a request runs", () => {
  const locked = (busy: boolean) =>
    mount(AuthDialog, {
      attachTo: document.body,
      props: { busy },
      slots: {
        default: () => [
          h(
            "button",
            { class: "auth-action", type: "button" },
            "Нет доступа к почте?",
          ),
          h(
            "button",
            { class: "auth-button", type: "submit", disabled: busy },
            "Отправить письмо",
          ),
        ],
      },
    });

  it("dims the cross and lets it close nothing", async () => {
    const wrapper = locked(true);
    const cross = wrapper.get("button.auth-close");

    expect(cross.attributes("aria-disabled")).toBe("true");
    // aria-disabled, not disabled: the caret waits on it.
    expect(cross.attributes("disabled")).toBeUndefined();
    await cross.trigger("click");
    expect(wrapper.emitted("close")).toBeUndefined();

    await wrapper.setProps({ busy: false });
    expect(cross.attributes("aria-disabled")).toBeUndefined();
    await cross.trigger("click");
    expect(wrapper.emitted("close")).toHaveLength(1);
  });

  it("tells the library that Escape and a click beside the card close nothing", async () => {
    const wrapper = locked(true);
    expect(library.props).toMatchObject({
      clickToClose: false,
      escToClose: false,
    });

    await wrapper.setProps({ busy: false });
    expect(library.props).toMatchObject({
      clickToClose: true,
      escToClose: true,
    });
  });

  it("holds the card's links until the answer", async () => {
    const pressed = vi.fn();
    const wrapper = locked(true);
    wrapper.get(".auth-action").element.addEventListener("click", pressed);

    await wrapper.get(".auth-action").trigger("click");
    expect(pressed).not.toHaveBeenCalled();

    await wrapper.setProps({ busy: false });
    await wrapper.get(".auth-action").trigger("click");
    expect(pressed).toHaveBeenCalledOnce();
  });

  it("keeps the caret on the cross while the pressed button is shut, then gives it back", async () => {
    const wrapper = mount(
      defineComponent({
        props: { busy: Boolean },
        setup: (props) => () =>
          h(AuthDialog, { busy: props.busy }, () =>
            h(
              "button",
              { class: "auth-button", disabled: props.busy },
              "Войти",
            ),
          ),
      }),
      { attachTo: document.body, props: { busy: false } },
    );
    const button = wrapper.get("button.auth-button")
      .element as HTMLButtonElement;
    button.focus();

    await wrapper.setProps({ busy: true });
    expect(document.activeElement).toBe(
      wrapper.get("button.auth-close").element,
    );

    await wrapper.setProps({ busy: false });
    await nextTick();
    expect(document.activeElement).toBe(button);
  });
});
