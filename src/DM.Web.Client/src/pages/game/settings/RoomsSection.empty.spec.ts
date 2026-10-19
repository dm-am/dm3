/**
 * @vitest-environment jsdom
 */

// The table of rooms before and without any: it waits for the list to answer,
// says a refusal of it, and says "no rooms" only once the list came back empty.
import { describe, it, expect, afterEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { useGameDetailsStore } from "@/entities/game";
import { mountSection } from "./RoomsSection.fixtures";

afterEach(() => {
  vi.restoreAllMocks();
});

describe("RoomsSection without rooms", () => {
  it("has no table to save while the game has no rooms", async () => {
    const wrapper = mountSection([]);
    await flushPromises();

    expect(wrapper.text()).toContain("Комнат пока нет");
    expect(wrapper.find(".save-all").exists()).toBe(false);
  });

  // Before the list answers, an empty one is not "no rooms".
  it("stands skeleton rows, and no words, until the rooms answer", async () => {
    let answerRooms = () => {};
    const wrapper = mountSection(
      [],
      [],
      new Promise<void>((done) => (answerRooms = done)),
    );
    await flushPromises();

    expect(wrapper.findAll(".rooms-waiting .row-skeleton")).toHaveLength(2);
    expect(wrapper.text()).not.toContain("Комнат пока нет");

    answerRooms();
    await flushPromises();
    expect(wrapper.find(".rooms-waiting").exists()).toBe(false);
    expect(wrapper.text()).toContain("Комнат пока нет");
  });

  it("says the refusal of the list with a retry, not that there are no rooms", async () => {
    const wrapper = mountSection([]);
    useGameDetailsStore().roomsError = "Не удалось загрузить комнаты";
    await flushPromises();

    expect(wrapper.get(".error-state .error-message").text()).toBe(
      "Не удалось загрузить комнаты",
    );
    expect(wrapper.text()).not.toContain("Комнат пока нет");
    await wrapper.get(".error-state .error-retry").trigger("click");
    expect(useGameDetailsStore().loadRooms).toHaveBeenLastCalledWith(
      "the-game",
    );
  });
});
