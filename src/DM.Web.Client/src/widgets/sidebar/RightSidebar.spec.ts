/**
 * @vitest-environment jsdom
 */

/** What the right column holds, every block stood in for by an item that names it. */
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";

const { block } = vi.hoisted(() => ({
  /** A block of the column as a list item that says which one it is. */
  block: async (name: string) => {
    const { defineComponent, h } = await import("vue");
    return {
      // Read as a module by the lazy loaders, which unwrap its default.
      __esModule: true,
      default: defineComponent({
        name,
        setup: () => () => h("li", { "data-block": name }),
      }),
    };
  },
}));

vi.mock("./SiteAddresses.vue", () => block("SiteAddresses"));
vi.mock("./ActivePolls.vue", () => block("ActivePolls"));
vi.mock("./PopularGames.vue", () => block("PopularGames"));
vi.mock("./PopularBlogs.vue", () => block("PopularBlogs"));
vi.mock("./GameTagCloud.vue", () => block("GameTagCloud"));
vi.mock("./ContactLinks.vue", () => block("ContactLinks"));
vi.mock("./SupportPanel.vue", () => block("SupportPanel"));
vi.mock("./PartnersPanel.vue", () => block("PartnersPanel"));
vi.mock("./SocialsPanel.vue", () => block("SocialsPanel"));

import RightSidebar from "./RightSidebar.vue";

const HEAD = [
  "SiteAddresses",
  "ActivePolls",
  "ContactLinks",
  "PopularGames",
  "PopularBlogs",
  "GameTagCloud",
];
const TAIL = ["SupportPanel", "PartnersPanel", "SocialsPanel"];

let column: VueWrapper | null = null;

afterEach(() => {
  column?.unmount();
  column = null;
});

/** The blocks of the column, top to bottom, and the name of its landmark. */
async function mountColumn() {
  column = mount(RightSidebar);
  await flushPromises();
  return {
    blocks: column
      .findAll("[data-block]")
      .map((el) => el.attributes("data-block")),
    label: column.get("nav").attributes("aria-label"),
  };
}

describe("the right column", () => {
  it("ends with the support, the fundraising, the partners and the socials", async () => {
    expect(await mountColumn()).toEqual({
      blocks: [...HEAD, ...TAIL],
      label: "Опросы, игры, блоги и контакты",
    });
  });

  it("stands every block in a list", async () => {
    await mountColumn();
    const loose = column!
      .findAll("[data-block]")
      .filter((item) => item.element.parentElement?.tagName !== "UL");
    expect(loose).toEqual([]);
  });
});
