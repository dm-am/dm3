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
  type GamePremoderationStatus,
  type Room,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";

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

function mountPanel(
  participation: GameParticipation[] = [],
  premoderationStatus?: GamePremoderationStatus,
) {
  const store = useGameDetailsStore();
  store.game = {
    ...game,
    participation,
    premoderationStatus,
  } as unknown as Game;
  store.rooms = rooms;
  // A non-empty slice keeps the mount off the network: the panel loads
  // characters only when it holds none.
  store.characters = [
    { id: "c-1", name: "Гоблин", isNpc: false, status: "Active" },
  ] as unknown as Character[];
  return mount(GamePanel, { props: { gameId: "abcde" }, global: { stubs } });
}

/** Sign in, optionally with a site-wide role. Must precede mountPanel. */
function signedInAs(role?: UserRole) {
  const auth = useAuthStore();
  auth.user = {
    username: "Кто-то",
    role,
  } as unknown as NonNullable<typeof auth.user>;
}

/**
 * What a row copies as: a non-breaking space is still a space, and a run of
 * whitespace is one space \u2014 the markup indents a button's caption onto its own
 * line, and the browser collapses that the same way this does.
 */
const copied = (text: string) =>
  text
    .replace(/\u00a0/g, " ")
    .replace(/\s+/g, " ")
    .trim();

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
  it("gives the curating mentor the settings link", () => {
    const menu = rows(mountPanel([GameParticipation.Moderator]));

    expect(menu).toContain("- Настройки");
  });

  // The notepad of the game is narrower than the settings page:
  // NotepadIntentionResolver answers it with HasEditAccess, which is master and
  // assistant only. The row used to be offered to a mentor and refused by the
  // API on arrival.
  it("keeps the notes of the game away from the curating mentor", () => {
    expect(rows(mountPanel([GameParticipation.Moderator]))).not.toContain(
      "- Заметки игры",
    );
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
    expect(menu).not.toContain("- Заметки игры");
  });

  it("keeps every edit item for the master", () => {
    const menu = rows(mountPanel([GameParticipation.Owner]));

    expect(menu).toContain("- Настройки");
    expect(menu).toContain("- Создать NPC");
    expect(menu).toContain("- Заметки игры");
  });

  // Premoderation. The server grants exactly three moves and grants them to
  // two different audiences; a row the panel shows to anybody else is a
  // promise the server answers with 403 or 400, which is the defect this
  // whole wave has been catching.
  describe("premoderation", () => {
    const SUBMIT = "- Отправить на проверку";
    const APPROVE = "- Одобрить игру";
    const RETURN = "- Вернуть на доработку";

    it("offers the master the submit row while the game is on edits", () => {
      expect(
        rows(mountPanel([GameParticipation.Owner], "AwaitingEdits")),
      ).toContain(SUBMIT);
    });

    it.each([
      ["AwaitingApproval", "AwaitingApproval" as GamePremoderationStatus],
      ["Approved", "Approved" as GamePremoderationStatus],
      ["not sent at all", undefined],
    ])(
      "hides the submit row from the master when the status is %s",
      (_name, status) => {
        expect(
          rows(mountPanel([GameParticipation.Owner], status)),
        ).not.toContain(SUBMIT);
      },
    );

    // GameIntention.SubmitForApproval admits the master alone. An assistant
    // fills the form in and the curating mentor helps shape it; neither of
    // them declares the game ready.
    it.each([
      ["an assistant", GameParticipation.Authority],
      ["the curating mentor", GameParticipation.Moderator],
      ["a player", GameParticipation.Player],
    ])("hides the submit row from %s on edits", (_name, participation) => {
      expect(rows(mountPanel([participation], "AwaitingEdits"))).not.toContain(
        SUBMIT,
      );
    });

    it("hides the submit row from a site mentor who is not the master", () => {
      signedInAs(UserRole.Mentor);
      expect(rows(mountPanel([], "AwaitingEdits"))).not.toContain(SUBMIT);
    });

    // Both verdicts are legal from every status on the server, so neither row
    // is keyed on the status the game happens to be in.
    it.each([
      ["Approved" as GamePremoderationStatus],
      ["AwaitingApproval" as GamePremoderationStatus],
      ["AwaitingEdits" as GamePremoderationStatus],
    ])("gives a site mentor both verdicts from %s", (status) => {
      signedInAs(UserRole.Mentor);
      const menu = rows(mountPanel([], status));

      expect(menu).toContain(APPROVE);
      expect(menu).toContain(RETURN);
    });

    it("keeps the verdicts away from a signed-in regular user", () => {
      signedInAs(UserRole.RegularUser);
      const menu = rows(mountPanel([GameParticipation.Owner], "AwaitingEdits"));

      expect(menu).not.toContain(APPROVE);
      expect(menu).not.toContain(RETURN);
    });

    // The mentor-only round trip the machine no longer has. Its two rows
    // named transitions the server has deleted, so both were a guaranteed 400.
    it("no longer offers the old take-in / release pair", () => {
      signedInAs(UserRole.Admin);
      const menu = rows(mountPanel([GameParticipation.Owner], "AwaitingEdits"));

      expect(menu).not.toContain("- Отправить на премодерацию");
      expect(menu).not.toContain("- Выпустить из премодерации");
    });
  });
});
