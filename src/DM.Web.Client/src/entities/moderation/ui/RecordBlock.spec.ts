/**
 * @vitest-environment jsdom
 */

// The words of a record break while "Перенос слов" of the settings panel is on.
import { describe, it, expect } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useUiStore } from "@/shared/stores/ui";
import RecordBlock from "./RecordBlock.vue";

describe("the text of a record", () => {
  it("follows the switch of the settings panel", async () => {
    localStorage.clear();
    const wrapper = mount(RecordBlock, {
      global: { plugins: [createPinia()] },
      slots: { default: "Текст записи" },
    });
    const text = () => wrapper.get(".record-block__text").classes();
    expect(text()).toContain("hyphenated");

    useUiStore().setHyphenateWords(false);
    await flushPromises();

    expect(text()).not.toContain("hyphenated");
  });

  it("breaks its words where no store is installed, as the switch does by default", () => {
    setActivePinia(undefined);
    const wrapper = mount(RecordBlock, { slots: { default: "Текст записи" } });

    expect(wrapper.get(".record-block__text").classes()).toContain(
      "hyphenated",
    );
  });
});
