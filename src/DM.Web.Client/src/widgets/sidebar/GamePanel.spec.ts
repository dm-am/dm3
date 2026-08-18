/**
 * @vitest-environment jsdom
 */

/**
 * The game menu is a composition, and the composition is the requirement:
 * one fixed heading, the two room groups with their rooms nested under them,
 * then the game's pages, each with a counter and no exception at zero.
 *
 * It had drifted on every one of those at once. The heading was the game's
 * title; the groups were bold captions rather than menu rows; four rows out
 * of five carried no counter; "Информация" and "Рецензии" were absent though
 * both routes exist; and a second list, rendered from
 * GET games/{id}/chat-rooms over the same rooms the first list already had,
 * drew every chat room a second time.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import GamePanel from "./GamePanel.vue";
import {
  GameParticipation,
  useGameDetailsStore,
  type Character,
  type Game,
  type Room,
} from "@/entities/game";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
  ConfirmDialog: true,
  GameStatusButtons: true,
  GameJoinActions: true,
};

const game = {
  id: "g-1",
  publicId: "abcde",
  title: "Хроники Забытых Королевств",
  participation: [],
  unreadCommentsCount: 0,
  unreadCharactersCount: 2,
  gameReviewsCount: 1,
  postReviewsCount: 7,
} as unknown as Game;

const rooms = [
  {
    id: "r-1",
    roomNumber: 1,
    title: "Таверна",
    type: "Default",
    access: "Open",
    unreadPostsCount: 5,
  },
  {
    id: "r-2",
    roomNumber: 4,
    title: "Пролог",
    type: "Default",
    access: "Open",
    unreadPostsCount: 0,
    isArchived: true,
  },
] as unknown as Room[];

function mountPanel(participation: GameParticipation[] = []) {
  const store = useGameDetailsStore();
  store.game = { ...game, participation } as unknown as Game;
  store.rooms = rooms;
  // A non-empty slice keeps the mount off the network: the panel loads
  // characters only when it holds none.
  store.characters = [
    { id: "c-1", name: "Гоблин", isNpc: false, status: "Active" },
  ] as unknown as Character[];
  return mount(GamePanel, { props: { gameId: "abcde" }, global: { stubs } });
}

/** What a row copies as: a non-breaking space is still a space. */
const copied = (text: string) => text.replace(/\u00a0/g, " ").trim();

/** Every menu row, in the order the menu renders them. */
function rows(wrapper: ReturnType<typeof mountPanel>) {
  return wrapper.findAll("li.link").map((li) => copied(li.text()));
}

describe("GamePanel", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("heads the block with the menu's own name, not the game's", () => {
    const wrapper = mountPanel();
    expect(wrapper.find(".sidebar-title").text()).toBe("Меню игры");
    expect(wrapper.find(".toggle").attributes("aria-label")).toBe(
      'Свернуть раздел "Меню игры"',
    );
  });

  it("lists the menu in one order, counters and all", () => {
    expect(rows(mountPanel())).toEqual([
      "- Активные комнаты",
      "Таверна (5)",
      "- Архивные комнаты (показать)",
      "- Информация",
      "- Обсуждение (0)",
      "- Персонажи (2)",
      "- Рецензии (1)",
      "- Оцененные посты (7)",
    ]);
  });

  it("nests the rooms under their group and draws each of them once", () => {
    const wrapper = mountPanel();
    const nested = wrapper.findAll("ul.room-list");
    expect(nested).toHaveLength(1);
    expect(nested[0].findAll("li.link")).toHaveLength(1);
    expect(wrapper.text().match(/Таверна/g)).toHaveLength(1);
  });

  it("keeps archived rooms behind the spoiler", () => {
    expect(mountPanel().text()).not.toContain("Пролог");
  });

  // GameIntention.EditSettings admits the curating mentor, so the menu has to
  // carry the link: without it the page is reachable only by typing the URL,
  // and the audience the intention was widened for never sees it.
  it("gives the curating mentor the settings link and the notepad", () => {
    const menu = rows(mountPanel([GameParticipation.Moderator]));

    expect(menu).toContain("- Настройки");
    expect(menu).toContain("- Блокнот мастера");
  });

  it("keeps the game's own edit items away from the curating mentor", () => {
    // The settings link rides EditSettings; NPCs and the status strip are the leads'.
    expect(rows(mountPanel([GameParticipation.Moderator]))).not.toContain(
      "- Создать NPC",
    );
  });

  it("keeps the management block away from a plain player", () => {
    const menu = rows(mountPanel([GameParticipation.Player]));

    expect(menu).not.toContain("- Настройки");
    expect(menu).not.toContain("- Блокнот мастера");
  });

  it("keeps every edit item for the master", () => {
    const menu = rows(mountPanel([GameParticipation.Owner]));

    expect(menu).toContain("- Настройки");
    expect(menu).toContain("- Создать NPC");
    expect(menu).toContain("- Блокнот мастера");
  });
});
