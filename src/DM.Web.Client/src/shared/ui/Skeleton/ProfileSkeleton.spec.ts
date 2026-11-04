/**
 * The bar of a page title is drawn once: the profile's placeholder takes the
 * one of PageTitleSkeleton, which holds PageTitle's line box and margins, and
 * keeps no bar of another size of its own.
 */
import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import PageTitleSkeleton from "./PageTitleSkeleton.vue";
import ProfileSkeleton from "./ProfileSkeleton.vue";

describe("ProfileSkeleton", () => {
  it("draws the title bar of every page", () => {
    const wrapper = mount(ProfileSkeleton);

    expect(wrapper.findAllComponents(PageTitleSkeleton)).toHaveLength(1);
    expect(wrapper.findAll(".skeleton-title")).toHaveLength(1);
    wrapper.unmount();
  });
});
