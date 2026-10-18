/**
 * @vitest-environment jsdom
 */

/**
 * What the button owes a request in flight.
 *
 * `loading` is two things at once and both are load-bearing: the control stops
 * answering, and it says so on screen. Fifteen pages used to fill the second
 * half by hand with a caption of their own; the caption is now whatever the
 * caller passed and nothing else.
 *
 * On screen a request in flight IS the disabled state — a control that has
 * stopped answering, hatched, in the fill it already had. What the two states
 * do not share is what they announce: `aria-busy` says the control will come
 * back, and a merely disabled one does not carry it.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import Button from "./Button.vue";

describe("Button", () => {
  it("marks a request in flight on the element the style reads", () => {
    const wrapper = mount(Button, {
      props: { loading: true },
      slots: { default: "Сохранить" },
    });

    expect(wrapper.attributes("aria-busy")).toBe("true");
    expect(wrapper.attributes("disabled")).toBeDefined();
  });

  it("keeps the caption the caller passed while the request is in flight", () => {
    const wrapper = mount(Button, {
      props: { loading: true },
      slots: { default: "Сохранить" },
    });

    expect(wrapper.text()).toBe("Сохранить");
  });

  it("leaves a merely disabled control unmarked", () => {
    const wrapper = mount(Button, {
      props: { disabled: true },
      slots: { default: "Сохранить" },
    });

    expect(wrapper.attributes("aria-busy")).toBeUndefined();
    expect(wrapper.attributes("disabled")).toBeDefined();
  });
});
