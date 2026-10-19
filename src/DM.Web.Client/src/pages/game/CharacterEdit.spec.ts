/**
 * @vitest-environment jsdom
 */

// Which sheet the page stands on and whether it draws the form for it.
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { announceWorded } from "@/shared/lib/errors";
import { problem } from "@/shared/lib/testing/problem";
import { endOfTask } from "@/shared/lib/testing/tasks";
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

// Every line on the page besides the status: none stands for a form withheld.
function linesBesideStatus(
  wrapper: ReturnType<typeof render>["wrapper"],
): string[] {
  return wrapper
    .findAll(".secondary-text:not(.status-value), .refusal-note")
    .map((line) => line.text());
}

// Drawn only for more than one character, so empty is also "no selector".
function namesInSelector(
  wrapper: ReturnType<typeof render>["wrapper"],
): string[] {
  return wrapper
    .findAll("select#character-switch option")
    .map((option) => option.text());
}

describe("CharacterEdit", () => {
  beforeEach(resetPage);

  // The form holds its place while the sheet is read, and says nothing meanwhile.
  it("stands the skeleton of the form until the sheet answers", async () => {
    // The read answers on the next turn: the first render is the wait.
    const { wrapper } = render({ participation: ["Player"] });

    expect(wrapper.find(".form-skeleton").exists()).toBe(true);
    expect(linesBesideStatus(wrapper)).toEqual([]);

    await flushPromises();
    expect(wrapper.find(".form-skeleton").exists()).toBe(false);
  });

  it("stands the skeleton, not 'no characters', while the roster loads", async () => {
    const { wrapper, store } = render();
    store.characters = [];
    store.charactersLoading = true;
    await nextTick();

    expect(wrapper.find(".form-skeleton").exists()).toBe(true);
    expect(wrapper.find(".empty").exists()).toBe(false);
  });

  // The roster the server answers holds no declined application, the master's
  // included: the page stands on what is left.
  it("lets a declined application leave the page with the roster", async () => {
    const change = changesStatus();

    const stranger = character({
      id: "char-2",
      name: "Странник",
      author: OTHER,
    });
    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
      others: [stranger],
    });
    await flushPromises();

    expect(namesInSelector(wrapper)).toEqual(["Ронин", "Странник"]);

    await press(wrapper, "Отклонить заявку");

    expect(change).toHaveBeenCalledWith("char-1", "Decline", FLAGS);
    expect(namesInSelector(wrapper)).toEqual([]);
    expect(routed.replace).toHaveBeenCalledWith({
      name: "game-character-edit",
      params: { id: "the-game", characterId: "char-2" },
    });
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(saidOutLoud()).toEqual([]);
  });

  it("has nothing left to edit once the only application is declined", async () => {
    changesStatus();

    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
    });
    await flushPromises();

    await press(wrapper, "Отклонить заявку");

    expect(wrapper.text()).toContain(
      "Нет персонажей, доступных для редактирования",
    );
    expect(wrapper.get(".status-section").isVisible()).toBe(false);
    expect(routed.push).not.toHaveBeenCalled();
  });

  // The page says the reason in its own line and takes the plate down; a
  // session that ran out is the plate's to say.
  it.each([
    ["the server's refusal", problem(404, "Персонаж не найден"), 0],
    ["a session that ran out", problem(401), 1],
  ])(
    "answers %s of the sheet's read where it belongs",
    async (_, failure, plates) => {
      const raise = vi.fn();
      announceWorded(failure, raise);

      const { wrapper } = render({ read: { data: null, error: failure } });
      await flushPromises();
      await endOfTask();

      expect(wrapper.get(".error").text()).toBe(
        failure.title || "Не удалось загрузить персонажа",
      );
      expect(raise).toHaveBeenCalledTimes(plates);
    },
  );

  // The sheet is the master's only where the player lets him (editByMaster).
  describe("a player's character, in front of the master", () => {
    it("has no form and no word about it where the player keeps the sheet", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({ author: OTHER }),
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(false);
      expect(linesBesideStatus(wrapper)).toEqual([]);
      expect(statusInWords(wrapper)).toBe("В игре");
      expect(wrapper.findAll(".status-btn").length).toBeGreaterThan(0);
    });

    it("has the form where the player lets the master edit the sheet", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          author: OTHER,
          privacy: {
            isNpc: false,
            editByMaster: true,
            editPostByMaster: false,
          },
        }),
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(true);
    });
  });

  // The blacklist closes the owner's application: editing it is applying again.
  describe("on the blacklist", () => {
    it("leaves the owner's application closed, without a word about it", async () => {
      const { wrapper } = render({
        subject: character({ status: "UnderReview" }),
        blacklisted: true,
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(false);
      expect(linesBesideStatus(wrapper)).toEqual([]);
      expect(buttons(wrapper)).toEqual(["Удалить персонажа"]);
    });

    it("leaves the sheet of a character in play open to the owner", async () => {
      const { wrapper } = render({ subject: character(), blacklisted: true });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(true);
    });
  });
});
