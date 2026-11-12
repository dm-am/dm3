/**
 * @vitest-environment jsdom
 */

// The polls block on the shell of the flat lists: its failure row and its link onward are the shell's.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { usePollsStore } from "@/entities/poll";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import ActivePolls from "./ActivePolls.vue";
import PollCardSkeleton from "./PollCardSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

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

  // Three title lines would stand in for some 600 px of polls, and the blocks below would drop when they come.
  it("holds the place of three poll cards while the polls load", () => {
    const { wrapper } = render({ polls: null });

    expect(wrapper.find(".sidebar-skeleton").exists()).toBe(false);
    const cards = wrapper.findAll("li.poll-skeleton");
    expect(cards).toHaveLength(3);
    for (const card of cards) {
      expect(card.attributes("aria-hidden")).toBe("true");
      // Title, "Активен до", four option bars, the kind of poll: PollCard's rows in its order.
      const rows = card.element.children;
      expect([...rows].map((row) => row.className.trim())).toEqual([
        "skeleton-row title",
        "skeleton-row status",
        "progress",
        "progress",
        "progress",
        "progress",
        "skeleton-row type",
      ]);
    }
  });

  // The skeleton repeats itself for a list (UI_STANDARDS/states.md), the caller only names how many.
  it("asks one placeholder for the three cards", () => {
    const { wrapper } = render({ polls: null });

    const placeholders = wrapper.findAllComponents(PollCardSkeleton);
    expect(placeholders).toHaveLength(1);
    expect(placeholders[0].props("count")).toBe(3);
  });

  it("sets the placeholder rows in the type of the card's rows", () => {
    const card = ruleMap(componentCss(resolve(HERE, "PollCard.vue")));
    const skeleton = ruleMap(
      componentCss(resolve(HERE, "PollCardSkeleton.vue")),
    );
    const row = skeleton.get(".skeleton-row");
    const pageType = ruleMap(
      componentCss(resolve(HERE, "SidebarSkeleton.vue")),
    );

    // A row is one line of the body type, as in the list placeholder.
    expect(row?.height).toBe("1.3em");
    expect(row?.["line-height"]).toBe(
      pageType.get(".skeleton-row")?.["line-height"],
    );
    expect(row?.["font-size"]).toBe(
      pageType.get(".skeleton-row")?.["font-size"],
    );
    for (const [part, real] of [
      [".skeleton-row.status", ".poll-status-inline"],
      [".skeleton-row.type", ".poll-type-indicator"],
    ]) {
      expect(skeleton.get(part)?.["font-size"], part).toBe(
        card.get(real)?.["font-size"],
      );
      expect(skeleton.get(part)?.["margin-top"], part).toBe(
        card.get(real)?.["margin-top"],
      );
    }
    expect(skeleton.get(".poll-skeleton")?.margin).toBe(
      `0 0 ${card.get(".poll")?.margin.split(" ")[0]}`,
    );
  });

  it("draws a card a row and leads on to every poll", () => {
    const { wrapper } = render({ polls: [{ id: "p1" }, { id: "p2" }] });

    expect(wrapper.findAll("li > .poll-card")).toHaveLength(2);
    const onward = wrapper.getComponent(RouterLinkStub);
    expect(onward.text()).toBe("Все опросы");
    expect(onward.props("to")).toEqual({ name: "polls" });
  });
});
