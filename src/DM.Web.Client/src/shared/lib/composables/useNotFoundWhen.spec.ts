/**
 * @vitest-environment jsdom
 */

// The address of a refused page opens "not found" with the address kept, once.
import { describe, expect, it } from "vitest";
import { defineComponent, ref } from "vue";
import { flushPromises } from "@vue/test-utils";
import type { RouteLocationNormalized } from "vue-router";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { useNotFoundWhen } from "./useNotFoundWhen";

const ADDRESS = "/games/the-game/settings?tab=rooms#top";

const routes: Address[] = [
  ["game", "/games/:gameId"],
  ["game-settings", "/games/:gameId/settings"],
  ["not-found", "/:pathMatch(.*)*"],
];

/** Mounts a page using the composable; `arrivals` lists every navigation after the mount. */
const mountWith = async (refused: boolean, at = ADDRESS) => {
  const flag = ref(refused);
  const arrivals: RouteLocationNormalized[] = [];
  const Shell = defineComponent({
    setup() {
      useNotFoundWhen(flag);
      return () => null;
    },
  });
  const { router } = await mountPage(Shell, at, {
    routes,
    beforeMount: (r) => r.afterEach((to) => void arrivals.push(to)),
  });
  return { flag, arrivals, router };
};

describe("useNotFoundWhen", () => {
  it("leaves a page that is not refused where it is", async () => {
    const { arrivals, router } = await mountWith(false);
    await flushPromises();

    expect(arrivals).toHaveLength(0);
    expect(router.currentRoute.value.name).toBe("game-settings");
  });

  it("opens not found at the address of a page refused from the start", async () => {
    const { arrivals, router } = await mountWith(true);
    await flushPromises();

    expect(arrivals).toHaveLength(1);
    const route = router.currentRoute.value;
    expect(route.name).toBe("not-found");
    expect(route.params).toEqual({
      pathMatch: ["games", "the-game", "settings"],
    });
    expect(route.query).toEqual({ tab: "rooms" });
    expect(route.hash).toBe("#top");
    expect(route.fullPath).toBe(ADDRESS);
  });

  it("opens not found when the refusal arrives later", async () => {
    const { flag, arrivals, router } = await mountWith(false);
    flag.value = true;
    await flushPromises();

    expect(arrivals).toHaveLength(1);
    expect(router.currentRoute.value.name).toBe("not-found");
  });

  // Not found takes the refused page's place: Back skips it for the page before.
  it("replaces the refused page in the history", async () => {
    const { flag, router } = await mountWith(false, "/games/the-game");
    await router.push(ADDRESS);
    flag.value = true;
    await flushPromises();
    expect(router.currentRoute.value.name).toBe("not-found");

    router.back();
    await flushPromises();

    expect(router.currentRoute.value.name).toBe("game");
  });
});
