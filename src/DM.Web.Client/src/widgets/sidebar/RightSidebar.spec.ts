/**
 * @vitest-environment jsdom
 */

/**
 * What the right column holds at each width. On a desktop it is the column it
 * always was: the addresses, the polls, the popular games and blogs, the tags,
 * and the four blocks that end it (RightSidebarTail). Below the shell
 * breakpoint the column slides in, holding the same blocks, and the documents
 * of the footer close it there. The four blocks never leave the column: the
 * shell stands none of them under the page.
 *
 * Each block is stood in for by a list item that names it, so the order of
 * the column reads straight off the mount.
 */
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
vi.mock("./LegalLinks.vue", () => block("LegalLinks"));
vi.mock("./ContactLinks.vue", () => block("ContactLinks"));
vi.mock("./SupportPanel.vue", () => block("SupportPanel"));
vi.mock("./PartnersPanel.vue", () => block("PartnersPanel"));
vi.mock("./SocialsPanel.vue", () => block("SocialsPanel"));

/** The width the column is told it has, as useShellMode would read it. */
const width = vi.hoisted(() => ({ narrow: false }));

vi.mock("@/shared/lib/composables/useShellMode", async () => {
  const { computed } = await import("vue");
  return {
    useShellMode: () => ({
      narrow: computed(() => width.narrow),
    }),
  };
});

import RightSidebar from "./RightSidebar.vue";

const HEAD = [
  "SiteAddresses",
  "ActivePolls",
  "PopularGames",
  "PopularBlogs",
  "GameTagCloud",
];
const TAIL = ["ContactLinks", "SupportPanel", "PartnersPanel", "SocialsPanel"];

let column: VueWrapper | null = null;

afterEach(() => {
  column?.unmount();
  column = null;
});

/** The blocks of the column, top to bottom, and the name of its landmark. */
async function columnAt(narrow: boolean) {
  width.narrow = narrow;
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
  it("ends with the support, the fundraising, the partners and the socials on a desktop", async () => {
    expect(await columnAt(false)).toEqual({
      blocks: [...HEAD, ...TAIL],
      label: "Опросы, игры, блоги и контакты",
    });
  });

  it("keeps the four blocks below the shell breakpoint and ends with the documents", async () => {
    expect(await columnAt(true)).toEqual({
      blocks: [...HEAD, ...TAIL, "LegalLinks"],
      label: "Опросы, игры, блоги, контакты и документы",
    });
  });

  it("stands every block in a list", async () => {
    for (const narrow of [false, true]) {
      await columnAt(narrow);
      const loose = column!
        .findAll("[data-block]")
        .filter((item) => item.element.parentElement?.tagName !== "UL");
      expect(loose, String(narrow)).toEqual([]);
      column!.unmount();
      column = null;
    }
  });
});
