/**
 * @vitest-environment jsdom
 */

/**
 * "Аккаунт удален" is a name like any other.
 *
 * It stood grey (the site's .muted) wherever an author, a master or an owner
 * arrived as null, and the owner asked why (12 October 2026): it is to look
 * the way every other name of the place looks. So the link carries no colour
 * of its own and takes the link's, the way a UserLink does, and still leads to
 * the profile that gathers what removed accounts left.
 */
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

  it("is not greyed out: no class gives it a colour of its own", () => {
    const wrapper = render();

    expect(wrapper.classes()).not.toContain("muted");
    expect(wrapper.classes()).toEqual([]);
  });

  it("keeps a class its place gives it, the way any link does", () => {
    const wrapper = mount(RemovedAccountLink, {
      attrs: { class: "subscription-title" },
      global: { stubs: { RouterLink: RouterLinkStub } },
    });

    expect(wrapper.classes()).toEqual(["subscription-title"]);
  });
});
