/**
 * @vitest-environment jsdom
 */

/** The footer: the copyright and the credits, then the legal links. */
import { describe, expect, it } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import Footer from "./Footer.vue";

describe("the columns of the footer", () => {
  it("reads the copyright and the credits before the legal links", () => {
    const footer = mount(Footer, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    const parts = footer.findAll(".footer > *").map((el) => el.classes()[0]);
    expect(parts).toEqual(["credits", "legal"]);
    expect(footer.get(".credits > div").text()).toMatch(/^© Dungeon Master/);
    footer.unmount();
  });
});
