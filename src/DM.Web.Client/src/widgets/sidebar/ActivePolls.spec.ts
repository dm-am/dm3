/**
 * @vitest-environment jsdom
 */

// The polls block on the shell of the flat lists: its failure row and its link onward are the shell's.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { usePollsStore } from "@/entities/poll";
import ActivePolls from "./ActivePolls.vue";

function render(state: { polls: unknown[] | null; error?: string }) {
  const store = usePollsStore();
  const fetch = vi
    .spyOn(store, "fetchActivePolls")
    .mockResolvedValue(undefined as never);
  store.activePolls = state.polls as never;
  store.activePollsError = (state.error ?? null) as never;
  const wrapper = mount(ActivePolls, {
    global: {
      stubs: {
        RouterLink: RouterLinkStub,
        PollCard: { template: '<div class="poll-card" />', props: ["poll"] },
      },
    },
  });
  return { wrapper, fetch };
}

beforeEach(() => {
  setActivePinia(createPinia());
});

describe("ActivePolls", () => {
  it("says the polls did not come in the words of every flat list, and asks again past the cache", async () => {
    const { wrapper, fetch } = render({ polls: null, error: "сбой" });
    fetch.mockClear();

    const row = wrapper.get(".error-row");
    expect(row.text().replace(/\s+/g, " ")).toBe(
      "Не удалось загрузить Повторить",
    );
    await row.get("button").trigger("click");
    await flushPromises();

    expect(fetch).toHaveBeenCalledWith(true);
  });

  it("draws a card a row and leads on to every poll", () => {
    const { wrapper } = render({ polls: [{ id: "p1" }, { id: "p2" }] });

    expect(wrapper.findAll("li > .poll-card")).toHaveLength(2);
    const onward = wrapper.getComponent(RouterLinkStub);
    expect(onward.text()).toBe("Все опросы");
    expect(onward.props("to")).toEqual({ name: "polls" });
  });
});
