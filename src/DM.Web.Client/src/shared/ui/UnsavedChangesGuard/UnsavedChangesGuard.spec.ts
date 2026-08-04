/**
 * @vitest-environment jsdom
 */

/**
 * Half an hour of a character sheet, and a link in the sidebar that is on every
 * page of a game. The navigation was instant and silent and the sheet came back
 * empty; the client had no onBeforeRouteLeave and no beforeunload at all.
 *
 * A real router is used here, because the guard registers against the matched
 * route record and a stub would prove nothing about that.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createMemoryHistory, createRouter, RouterView } from "vue-router";
import UnsavedChangesGuard from "./UnsavedChangesGuard.vue";

const Guarded = defineComponent({
  props: { dirty: { type: Boolean, required: true } },
  setup(props) {
    return () => h(UnsavedChangesGuard, { dirty: props.dirty, ref: "guard" });
  },
});

const Elsewhere = defineComponent({ setup: () => () => h("div", "elsewhere") });

async function mountAt(dirty: boolean) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/form", component: Guarded, props: { dirty } },
      { path: "/elsewhere", component: Elsewhere },
    ],
  });
  router.push("/form");
  await router.isReady();

  // RouterView the component, not the string "router-view": h() with a string
  // builds a plain element, and the route component under it never mounts.
  const wrapper = mount(defineComponent({ setup: () => () => h(RouterView) }), {
    global: { plugins: [router] },
  });
  await flushPromises();

  return { router, wrapper };
}

describe("UnsavedChangesGuard", () => {
  beforeEach(() => vi.clearAllMocks());

  it("lets a clean form go without a word", async () => {
    const { router } = await mountAt(false);

    await router.push("/elsewhere");

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });

  it("holds the navigation and stays when the answer is no", async () => {
    const { router, wrapper } = await mountAt(true);

    const leaving = router.push("/elsewhere");
    await flushPromises();
    expect(router.currentRoute.value.path).toBe("/form");

    wrapper.findComponent(UnsavedChangesGuard).vm.answer(false);
    await leaving;
    await flushPromises();

    expect(router.currentRoute.value.path).toBe("/form");
  });

  it("lets the navigation through when the answer is yes", async () => {
    const { router, wrapper } = await mountAt(true);

    const leaving = router.push("/elsewhere");
    await flushPromises();

    wrapper.findComponent(UnsavedChangesGuard).vm.answer(true);
    await leaving;
    await flushPromises();

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });
});
