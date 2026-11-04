/**
 * @vitest-environment jsdom
 */

// The tag cloud says a failed load as the flat lists say it.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGamesStore } from "@/entities/game";
import GameTagCloud from "./GameTagCloud.vue";

beforeEach(() => {
  setActivePinia(createPinia());
});

describe("GameTagCloud", () => {
  it("says the tags did not come in the words of every flat list, and asks again past the cache", async () => {
    const store = useGamesStore();
    const fetch = vi
      .spyOn(store, "fetchTags")
      .mockResolvedValue(undefined as never);
    store.tags = null as never;
    store.tagsLoading = false as never;
    store.tagsError = "сбой" as never;
    const wrapper = mount(GameTagCloud, {
      global: { stubs: { RouterLink: RouterLinkStub } },
    });
    fetch.mockClear();

    const row = wrapper.get(".secondary-text");
    expect(row.text().replace(/\s+/g, " ")).toBe(
      "Не удалось загрузить Повторить",
    );
    await row.get("button").trigger("click");
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith(true);
  });
});
