import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import PasswordInput from "./PasswordInput.vue";

/**
 * The reveal toggle is the one control on a password field a person needs when
 * the typing went wrong, and it used to be mouse-only: tabindex="-1" took it
 * out of the tab order on every password form in the product. The label was
 * static too, so a screen reader announced both halves of the action and named
 * neither the current state nor the one a press leads to.
 */
describe("PasswordInput", () => {
  const toggle = (visible = false) =>
    mount(PasswordInput, {
      props: { modelValue: visible ? "secret" : "" },
    }).find("button.password-toggle");

  it("keeps the reveal toggle in the tab order", () => {
    expect(toggle().attributes("tabindex")).toBeUndefined();
  });

  it("names the action a press performs, and says which state it is in", async () => {
    const wrapper = mount(PasswordInput, { props: { modelValue: "secret" } });
    const button = wrapper.find("button.password-toggle");
    const input = wrapper.find("input");

    expect(input.attributes("type")).toBe("password");
    expect(button.attributes("aria-label")).toBe("Показать пароль");
    expect(button.attributes("aria-pressed")).toBe("false");

    await button.trigger("click");

    expect(input.attributes("type")).toBe("text");
    expect(button.attributes("aria-label")).toBe("Скрыть пароль");
    expect(button.attributes("aria-pressed")).toBe("true");
  });

  it("does not submit the form it sits in", () => {
    expect(toggle().attributes("type")).toBe("button");
  });
});
