/**
 * @vitest-environment jsdom
 */

/**
 * Deep links into the rules page: /rules#points has to open "Механика баллов",
 * not merely scroll past a closed row.
 *
 * Both halves of the arrival matter, and only one of them used to work:
 *
 *   - the document loads at the address (a link from outside, a reload) — the
 *     section component mounts with the hash already in place;
 *   - the hash changes inside a document that is already open (an address-bar
 *     paste onto the page one is standing on, an in-page anchor, back/forward
 *     between two anchors) — vue-router updates `route.hash` and nothing
 *     remounts, so anything that runs only in `onMounted` never runs again.
 *
 * Measured in Chromium against the running preview server before the fix:
 * a cold load of /rules#points expanded the row, while `location.hash =
 * "#points"` on an already-open /rules left `route.hash` at "#points" with
 * zero expanded rows.
 *
 * The two negative cases are the rule, not an accident: an id belonging to no
 * row (#nothing-here) and the anchor of a page SECTION (#bans, the <section>
 * wrapping RulesBans) must leave every row closed — four sections share one
 * hash, and without the check each would open a row of another one's list.
 */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub, type VueWrapper } from "@vue/test-utils";
import { createMemoryHistory, createRouter, type Router } from "vue-router";
import { createPinia } from "pinia";
import type { Component } from "vue";
import { nextTick } from "vue";
import RulesPage from "./RulesPage.vue";
import RulesBans from "./RulesBans.vue";
import RulesAuthors from "./RulesAuthors.vue";
import RulesExternalLinks from "./RulesExternalLinks.vue";

/** One row id per consumer of the composable — all four rules sections. */
const consumers = [
  { name: "RulesPage", component: RulesPage as Component, itemId: "hacking" },
  { name: "RulesBans", component: RulesBans as Component, itemId: "points" },
  {
    name: "RulesAuthors",
    component: RulesAuthors as Component,
    itemId: "games",
  },
  {
    name: "RulesExternalLinks",
    component: RulesExternalLinks as Component,
    itemId: "other-rpg",
  },
];

async function mountAt(component: Component, hash: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: "/:pathMatch(.*)*", component: { template: "<div />" } }],
  });
  await router.replace(`/rules${hash}`);
  await router.isReady();
  const wrapper = mount(component, {
    global: {
      plugins: [router, createPinia()],
      stubs: {
        // Links are not what is under test, and the staff table would reach
        // for the network on mount.
        RouterLink: RouterLinkStub,
        RulesStaffTable: true,
      },
    },
  });
  await nextTick();
  return { wrapper, router };
}

/** Ids of the rows currently open, read off the DOM the reader sees. */
function expandedIds(wrapper: VueWrapper): string[] {
  return wrapper
    .findAll(".expandable-row.expanded")
    .map((row) =>
      (row.attributes("id") ?? "").replace("expandable-toggle-", ""),
    );
}

async function navigate(router: Router, hash: string) {
  await router.push(`/rules${hash}`);
  await nextTick();
}

describe.each(consumers)(
  "useExpandOnHash in $name",
  ({ component, itemId }) => {
    it("opens the row named by the hash the document loaded with", async () => {
      const { wrapper } = await mountAt(component, `#${itemId}`);

      expect(expandedIds(wrapper)).toEqual([itemId]);
      expect(
        wrapper.get(`#expandable-toggle-${itemId}`).attributes("aria-expanded"),
      ).toBe("true");
    });

    it("opens the row when the hash arrives without a remount", async () => {
      const { wrapper, router } = await mountAt(component, "");
      expect(expandedIds(wrapper)).toEqual([]);

      await navigate(router, `#${itemId}`);

      expect(expandedIds(wrapper)).toEqual([itemId]);
    });

    it("opens nothing for a hash that names no row", async () => {
      const { wrapper, router } = await mountAt(component, "#nothing-here");
      expect(expandedIds(wrapper)).toEqual([]);

      await navigate(router, "#also-nothing");

      expect(expandedIds(wrapper)).toEqual([]);
    });

    it("opens nothing for the anchor of a page section", async () => {
      const { wrapper, router } = await mountAt(component, "#bans");
      expect(expandedIds(wrapper)).toEqual([]);

      await navigate(router, "#penalties");

      expect(expandedIds(wrapper)).toEqual([]);
    });
  },
);
