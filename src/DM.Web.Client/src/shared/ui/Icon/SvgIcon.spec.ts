/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import SvgIcon from "./SvgIcon.vue";
import { icons, type IconName } from "@/shared/lib/utils/icons";

describe("SvgIcon", () => {
  it("draws the icon it is named after", () => {
    const wrapper = mount(SvgIcon, { props: { name: "pencil" } });
    expect(wrapper.attributes("viewBox")).toBe(icons.pencil.viewBox);
  });

  it("draws a question mark instead of taking the page down", () => {
    // The cast is the point: a name that got past the type.
    const name = "inbox" as unknown as IconName;
    const wrapper = mount(SvgIcon, { props: { name } });
    expect(wrapper.attributes("viewBox")).toBe(icons.question.viewBox);
  });
});
