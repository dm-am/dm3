/**
 * @vitest-environment jsdom
 */

// The address of a refused page opens "not found" with the address kept, once.
import { describe, expect, it, vi } from "vitest";
import { defineComponent, nextTick, ref } from "vue";
import { mount } from "@vue/test-utils";

const replace = vi.hoisted(() => vi.fn());

vi.mock("vue-router", () => ({
  useRoute: () => ({
    path: "/games/the-game/settings",
    query: { tab: "rooms" },
    hash: "#top",
  }),
  useRouter: () => ({ replace }),
}));

import { useNotFoundWhen } from "./useNotFoundWhen";

const mountWith = (refused: boolean) => {
  const flag = ref(refused);
  mount(
    defineComponent({
      setup() {
        useNotFoundWhen(flag);
        return () => null;
      },
    }),
  );
  return flag;
};

describe("useNotFoundWhen", () => {
  it("leaves a page that is not refused where it is", async () => {
    replace.mockClear();
    mountWith(false);
    await nextTick();

    expect(replace).not.toHaveBeenCalled();
  });

  it("opens not found at the address of a page refused from the start", async () => {
    replace.mockClear();
    mountWith(true);
    await nextTick();

    expect(replace).toHaveBeenCalledTimes(1);
    expect(replace).toHaveBeenCalledWith({
      name: "not-found",
      params: { pathMatch: ["games", "the-game", "settings"] },
      query: { tab: "rooms" },
      hash: "#top",
    });
  });

  it("opens not found when the refusal arrives later", async () => {
    replace.mockClear();
    const flag = mountWith(false);
    flag.value = true;
    await nextTick();
    await nextTick();

    expect(replace).toHaveBeenCalledTimes(1);
  });
});
