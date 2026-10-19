/**
 * @vitest-environment jsdom
 */

// A request in flight looks disabled; only aria-busy says the control will come back.
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
