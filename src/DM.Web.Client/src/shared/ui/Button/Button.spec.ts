/**
 * @vitest-environment jsdom
 */

/**
 * What the button owes a request in flight.
 *
 * `loading` is two things at once and both are load-bearing: the control stops
 * answering, and it says on screen that something is happening. The second half
 * was missing, and fifteen pages filled the gap by hand with a caption of their
 * own. The style that draws it now is keyed off `aria-busy`, so the attribute
 * is not decoration for a screen reader here, it is the switch, and the caption
 * is whatever the caller passed and nothing else.
 *
 * `disabled` is a different state and stays one: a control that cannot be used
 * right now is not a control that is working.
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
