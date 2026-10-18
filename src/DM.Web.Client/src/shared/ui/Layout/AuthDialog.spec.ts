import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import AuthDialog from "./AuthDialog.vue";

/**
 * The cross is the visible way out of a guest dialog, and it is drawn rather
 * than typed.
 *
 * The owner chose the bars over the site's glyph: every typographic cross the
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
