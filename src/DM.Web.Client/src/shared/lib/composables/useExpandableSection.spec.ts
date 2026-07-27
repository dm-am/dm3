/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { defineComponent, h, ref } from "vue";
import { mount } from "@vue/test-utils";
import { useExpandableSection } from "./useExpandableSection";
import {
  expandAll,
  collapseAll,
  clearRegistry,
  hasAny,
} from "./useExpandableRegistry";

/** Host component: the composable needs a setup context for lifecycle. */
function mountSection(startExpanded = false) {
  let section!: ReturnType<typeof useExpandableSection>;
  const wrapper = mount(
    defineComponent({
      setup() {
        const el = ref<HTMLElement | null>(null);
        section = useExpandableSection({ el, startExpanded, label: "spec" });
        return () => h("div");
      },
    }),
  );
  return { wrapper, section };
}

describe("useExpandableSection", () => {
  beforeEach(() => clearRegistry());

  it("registers collapsed sections and follows the bulk toggle", () => {
    const { section } = mountSection();
    expect(hasAny.value).toBe(true);

    expandAll();
    expect(section.isExpanded.value).toBe(true);

    collapseAll();
    expect(section.isExpanded.value).toBe(false);
  });

  it("keeps started-expanded sections out of the registry", () => {
    const { section } = mountSection(true);
    expect(hasAny.value).toBe(false);

    collapseAll();
    expect(section.isExpanded.value).toBe(true);
  });

  it("unregisters on unmount", () => {
    const { wrapper } = mountSection();
    expect(hasAny.value).toBe(true);
    wrapper.unmount();
    expect(hasAny.value).toBe(false);
  });

  it("syncs a late-mounting section with a bulk expand in flight", () => {
    expandAll();
    const { section } = mountSection();
    expect(section.isExpanded.value).toBe(true);
  });

  it("manual toggle clears the pending bulk action for late registrants", () => {
    const first = mountSection();
    expandAll();
    // The user manually collapses one section — the bulk action is void.
    first.section.toggle(false);

    const late = mountSection();
    expect(late.section.isExpanded.value).toBe(false);
  });
});
