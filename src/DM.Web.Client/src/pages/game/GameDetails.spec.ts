/**
 * @vitest-environment jsdom
 */

// The two rosters are one table drawn twice: each keeps its own columns.
import { afterEach, describe, expect, it } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import GameDetails from "./GameDetails.vue";

enableAutoUnmount(afterEach);

const author = { id: "u-1", username: "игрок", role: "Player" };

const character = (
  over: Partial<Record<keyof Character, unknown>>,
): Character =>
  ({
    id: "c-1",
    name: "Ронин",
    status: "Active",
    isNpc: false,
    isDead: false,
    isPlayerLeft: false,
    isPlayerExiled: false,
    author,
    totalPostsCount: 3,
    ...over,
  }) as unknown as Character;

async function render(characters: Character[]) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "g-1",
    publicId: "the-game",
    status: "Active",
    schema: { specifications: [] },
  } as unknown as Game;
  store.characters = characters;

  const wrapper = mount(GameDetails, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });
  await flushPromises();
  return wrapper;
}

const rosters = (wrapper: Awaited<ReturnType<typeof render>>) =>
  wrapper.findAll(".roster").map((roster) => ({
    title: roster.get("h2").text(),
    columns: roster.findAll("th").map((th) => th.text()),
    rows: roster.findAll(".table-row").length,
  }));

describe("GameDetails rosters", () => {
  it("draws the players' and the master's characters each with its own columns", async () => {
    const wrapper = await render([
      character({ id: "c-1" }),
      character({ id: "c-2", isNpc: true, author: undefined }),
      character({ id: "c-3", status: "UnderReview" }),
    ]);

    // The master's characters are the same table without the player.
    const ofCharacter = [
      "Имя персонажа",
      "Класс",
      "Посты",
      "Последний пост",
      "Статус",
    ];
    expect(rosters(wrapper)).toEqual([
      {
        title: "Персонажи игроков",
        columns: ["#", "Игрок", "Рейтинг", "Присутствие", ...ofCharacter],
        rows: 1,
      },
      {
        title: "Неигровые персонажи",
        columns: ["#", ...ofCharacter],
        rows: 1,
      },
    ]);
  });

  it("leaves out a roster nobody is in", async () => {
    const wrapper = await render([character({ isNpc: true })]);

    expect(rosters(wrapper).map((roster) => roster.title)).toEqual([
      "Неигровые персонажи",
    ]);
  });

  it("centres every column of both rosters", async () => {
    const wrapper = await render([
      character({ id: "c-1" }),
      character({ id: "c-2", isNpc: true }),
    ]);

    const cells = wrapper.findAll(".roster .col:not(.col-number)");
    expect(cells.length).toBeGreaterThan(0);
    expect(cells.filter((cell) => !cell.classes("align-center"))).toEqual([]);
  });

  it("mutes a character out of the game, and only that one", async () => {
    const wrapper = await render([
      character({ id: "c-1", name: "Ронин" }),
      character({
        id: "c-2",
        name: "Странник",
        status: "Retired",
        isDead: true,
      }),
    ]);

    const rows = wrapper.findAll(".roster .table-row").map((row) => {
      const status = row.get(".col-status span");
      return {
        name: row.get(".col-character a").classes("retired"),
        status: [status.text(), status.classes()],
      };
    });
    expect(rows).toEqual([
      { name: false, status: ["В игре", ["status-active"]] },
      { name: true, status: ["Персонаж мертв", ["status-retired"]] },
    ]);
  });

  // The words and the order of the flags are the card's (characterStatusLabel).
  it("words a character out of the game as its card does", async () => {
    const wrapper = await render([
      character({
        id: "c-1",
        status: "Retired",
        isPlayerLeft: true,
        isPlayerExiled: true,
      }),
      character({ id: "c-2", status: "Retired" }),
    ]);

    expect(
      wrapper
        .findAll(".roster .table-row .col-status span")
        .map((cell) => cell.text()),
    ).toEqual(["Покинул игру", "Вне игры"]);
  });
});
