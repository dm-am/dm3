/**
 * @vitest-environment jsdom
 */

// A name like any other: no colour of its own.
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import RemovedAccountLink from "./RemovedAccountLink.vue";
import {
  REMOVED_ACCOUNT_NAME,
  removedAccountRoute,
} from "../lib/removedAccount";

const render = () =>
  mount(RemovedAccountLink, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });

describe("RemovedAccountLink", () => {
  it("says the name and leads to its profile", () => {
    const link = render().getComponent(RouterLinkStub);

    expect(link.text()).toBe(REMOVED_ACCOUNT_NAME);
    expect(link.props("to")).toEqual(removedAccountRoute);
  });

  it("keeps a class its place gives it, the way any link does", () => {
    const wrapper = mount(RemovedAccountLink, {
      attrs: { class: "subscription-title" },
      global: { stubs: { RouterLink: RouterLinkStub } },
    });

    expect(wrapper.classes()).toEqual(["subscription-title"]);
  });
});
