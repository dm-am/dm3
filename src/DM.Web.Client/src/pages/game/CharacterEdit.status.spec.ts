/**
 * @vitest-environment jsdom
 */

/**
 * The status section of the page (CharacterStatusSection) answers with no
 * plate: a deletion leaves for a roster without the character, a transition
 * writes the new status, read before and after the press.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { characterApi } from "@/entities/game";
import { routed } from "./CharacterEdit.doubles";
import {
  FLAGS,
  OTHER,
  buttons,
  changesStatus,
  character,
  press,
  render,
  resetPage,
  saidOutLoud,
  statusInWords,
} from "./CharacterEdit.fixtures";

vi.mock("vue-router", async (importOriginal) =>
  (await import("./CharacterEdit.doubles")).vueRouter(importOriginal),
);

describe("CharacterEdit", () => {
  beforeEach(resetPage);

  it.each([
    ["a character", "Удалить персонажа", "Ронин", {}],
    [
      "an NPC",
      "Удалить неигрового персонажа",
      "Трактирщик",
      {
        participation: ["Owner"],
        subject: character({
          name: "Трактирщик",
          isNpc: true,
          author: undefined,
          privacy: {
            isNpc: true,
            editByMaster: true,
            editPostByMaster: false,
          },
        }),
      },
    ],
  ])(
    "leaves a deleted %s for a roster without it, silently",
    async (_, label, name, options) => {
      const remove = vi
        .spyOn(characterApi, "deleteCharacter")
        .mockResolvedValue({ data: null, error: null } as never);

      const { wrapper, loadCharacters, roster, page } = render(options);
      await flushPromises();

      await press(wrapper, label);

      expect(remove).toHaveBeenCalledWith("char-1", FLAGS);
      expect(loadCharacters).toHaveBeenCalledWith("the-game");
      expect(routed.push).toHaveBeenCalledWith({
        name: "game-characters",
        params: { id: "the-game" },
      });
      expect(roster()).not.toContain(name);
      expect(saidOutLoud()).toEqual([]);
      // The window that asked closes with its own fade over the roster.
      expect(page()).toEqual({ empty: true, status: false, window: true });
    },
  );

  // Also what proves an empty list of plates above means nothing was said.
  it("says a refused deletion in the window it was asked in", async () => {
    vi.spyOn(characterApi, "deleteCharacter").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render();
    await flushPromises();

    await press(wrapper, "Удалить персонажа");

    expect(loadCharacters).not.toHaveBeenCalled();
    expect(routed.push).not.toHaveBeenCalled();
    expect(wrapper.get(".dialog-refusal").text()).toBe("Персонаж не найден");
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  it("writes an accepted application into the status, not into a plate", async () => {
    const change = changesStatus();

    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
      becomes: character({ status: "Active", author: OTHER }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("На рассмотрении");

    await press(wrapper, "Принять в игру");

    expect(change).toHaveBeenCalledWith("char-1", "Accept", undefined);
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the status legible when exiling leaves only the way back", async () => {
    const change = changesStatus();

    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ author: OTHER }),
      becomes: character({
        author: OTHER,
        status: "Retired",
        isPlayerExiled: true,
      }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("В игре");

    await press(wrapper, "Изгнать из игры");

    expect(change).toHaveBeenCalledWith("char-1", "Exile", FLAGS);
    expect(buttons(wrapper)).toEqual(["Вернуть в игру"]);
    expect(statusInWords(wrapper)).toBe("Выведен из игры");
    expect(saidOutLoud()).toEqual([]);
  });

  it("writes a character out of the game into the status of their own sheet", async () => {
    const change = changesStatus();

    const { wrapper } = render({
      subject: character(),
      becomes: character({ status: "Retired", isPlayerLeft: true }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("В игре");

    await press(wrapper, "Покинуть игру");

    expect(change).toHaveBeenCalledWith("char-1", "Leave", FLAGS);
    expect(statusInWords(wrapper)).toBe("Покинул игру");
    expect(saidOutLoud()).toEqual([]);
  });

  it("says a refused transition in the window it was asked in", async () => {
    vi.spyOn(characterApi, "changeCharacterStatus").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render({
      subject: character(),
      becomes: character({ status: "Retired", isPlayerLeft: true }),
    });
    await flushPromises();

    await press(wrapper, "Покинуть игру");

    expect(loadCharacters).not.toHaveBeenCalled();
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(wrapper.get(".dialog-refusal").text()).toBe("Персонаж не найден");
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  // No window to speak in: the refusal stands under the buttons.
  it("says a refused transition without a question under the buttons", async () => {
    vi.spyOn(characterApi, "changeCharacterStatus").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
    });
    await flushPromises();

    await press(wrapper, "Принять в игру");

    expect(loadCharacters).not.toHaveBeenCalled();
    expect(wrapper.get(".status-section .action-result").text()).toBe(
      "Персонаж не найден",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  // The master revives only the character he killed.
  describe("after the player left", () => {
    const leftTheGame = character({ status: "Retired", isPlayerLeft: true });

    it("offers the owner no way back", async () => {
      const { wrapper } = render({ subject: leftTheGame });
      await flushPromises();

      expect(buttons(wrapper)).toEqual(["Удалить персонажа"]);
    });

    it("offers the master no way back either, and revives only the dead", async () => {
      const left = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerLeft: true,
          author: OTHER,
        }),
      });
      await flushPromises();
      expect(buttons(left.wrapper)).not.toContain("Вернуть в игру");

      const dead = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isDead: true,
          author: OTHER,
        }),
      });
      await flushPromises();
      expect(buttons(dead.wrapper)).toContain("Вернуть в игру");
    });
  });

  // Exile is taken back by its own transition, not by the one for the dead.
  describe("after the master exiled the character", () => {
    it("brings the character back for the master", async () => {
      const change = changesStatus();
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerExiled: true,
          author: OTHER,
        }),
        becomes: character({ status: "Active", author: OTHER }),
      });
      await flushPromises();

      expect(buttons(wrapper)).toEqual(["Вернуть в игру"]);

      await press(wrapper, "Вернуть в игру");

      expect(change).toHaveBeenCalledWith("char-1", "Readmit", undefined);
      expect(statusInWords(wrapper)).toBe("В игре");
    });

    it("offers the exiled player no way back", async () => {
      const { wrapper } = render({
        subject: character({ status: "Retired", isPlayerExiled: true }),
      });
      await flushPromises();

      expect(buttons(wrapper)).not.toContain("Вернуть в игру");
    });

    it("offers the master nothing when the player had left as well", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerExiled: true,
          isPlayerLeft: true,
          author: OTHER,
        }),
      });
      await flushPromises();

      expect(buttons(wrapper)).not.toContain("Вернуть в игру");
    });
  });
});

// jsdom lays nothing out: the attributes of ButtonRow say "no scroll, no …".
describe("CharacterEdit row of transitions", () => {
  it("stands every transition in a row of buttons, none held back", async () => {
    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ author: OTHER }),
    });
    await flushPromises();

    const row = wrapper.get(".status-actions");
    expect(row.classes()).toContain("button-row");
    expect(row.attributes()).not.toHaveProperty("data-fit-row");
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    const transitions = wrapper
      .findAll(".status-btn")
      .map((b) => [b.text(), b.attributes("data-fit-keep") !== undefined]);
    expect(transitions).toEqual([
      ["Отметить погибшим", false],
      ["Изгнать из игры", false],
    ]);
  });
});
