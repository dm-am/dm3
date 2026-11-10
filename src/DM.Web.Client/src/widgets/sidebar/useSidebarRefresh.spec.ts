/**
 * @vitest-environment jsdom
 */

/** The three moments a sidebar list refetches; without the forced one the counters stay the last reader's. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { defineComponent, nextTick } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { useSidebarRefresh } from "./useSidebarRefresh";
import type { User } from "@/shared/api/models/common/user";

const routes: Address[] = [
  ["home", "/"],
  ["games", "/games"],
];

let pinia: Pinia;

const viewer = (username: string) => ({ id: username, username }) as User;

function mountBlock(fetch: (force?: boolean) => void) {
  const Shell = defineComponent({
    setup() {
      useSidebarRefresh(fetch);
      return () => null;
    },
  });
  return mountPage(Shell, "/", { routes, pinia });
}

describe("useSidebarRefresh", () => {
  beforeEach(() => {
    localStorage.clear();
    pinia = createPinia();
    setActivePinia(pinia);
  });

  it("reads the list once on mount, unforced", async () => {
    const fetch = vi.fn();
    await mountBlock(fetch);

    expect(fetch.mock.calls).toEqual([[]]);
  });

  it("forces a refetch when the viewer changes", async () => {
    const fetch = vi.fn();
    await mountBlock(fetch);
    fetch.mockClear();

    useAuthStore().updateUser(viewer("b"));
    await nextTick();

    expect(fetch.mock.calls).toEqual([[true]]);
  });

  it("asks again on navigation, unforced", async () => {
    const fetch = vi.fn();
    const { router } = await mountBlock(fetch);
    fetch.mockClear();

    await router.push("/games");
    await flushPromises();

    expect(fetch.mock.calls).toEqual([[]]);
  });
});
