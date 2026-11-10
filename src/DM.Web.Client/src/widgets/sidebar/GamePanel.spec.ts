/**
 * @vitest-environment jsdom
 */

/** The game menu: one heading, the room groups nested, every page with a counter. */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import GamePanel from "./GamePanel.vue";
import {
  CommentariesAccessMode,
  GameParticipation,
  discussionApi,
  useGameDetailsStore,
  type Game,
  type Room,
} from "@/entities/game";
import {
  createMockCharacter,
  createMockGame,
} from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { useAuthStore } from "@/entities/user";
import { UserRole, type PremoderationStatus } from "@/shared/api/models/common";

const stubs = {
  RouterLink: RouterLinkStub,
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
  ConfirmDialog: true,
  GameStatusButtons: true,
  GameJoinActions: true,
};

const game: Game = {
  ...createMockGame("g-1", "Хроники Забытых Королевств"),
  publicId: asServed("abcde"),
  participation: asServed([]),
  unreadCommentsCount: asServed(0),
  unreadCharactersCount: asServed(2),
  gameReviewsCount: asServed(1),
  postReviewsCount: asServed(7),
};

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
  premoderationStatus?: PremoderationStatus,
  overrides: Record<string, unknown> = {},
) {
  // Roles are served to a signed-in viewer alone: a participant here is signed in.
  const auth = useAuthStore();
  if (participation.length && !auth.user) signedInAs(UserRole.RegularUser);
  const store = useGameDetailsStore();
  store.game = {
    ...game,
    participation: asServed(participation),
    premoderationStatus,
  };
  store.rooms = rooms;
  // A non-empty slice keeps the mount off the network: the panel loads
  // characters only when it holds none.
  store.characters = [createMockCharacter("c-1", "Гоблин")];
  return mount(GamePanel, {
    props: { gameId: "abcde" },
    global: { stubs: { ...stubs, ...overrides } },
  });
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

  // The settings admit the curating mentor; the NPCs and the notepad are the master's and the assistants'.
  it.each([
    [
      "the master",
      GameParticipation.Owner,
      ["- Настройки", "- Создать неигрового персонажа", "- Заметки игры"],
      [],
    ],
    [
      "the curating mentor",
      GameParticipation.Moderator,
      ["- Настройки"],
      ["- Заметки игры", "- Создать неигрового персонажа"],
    ],
    [
      "a plain player",
      GameParticipation.Player,
      [],
      ["- Настройки", "- Заметки игры"],
    ],
  ])(
    "gives %s the management rows the server admits",
    (_name, participation, shown, hidden) => {
      const menu = rows(mountPanel([participation]));

      for (const row of shown) expect(menu).toContain(row);
      for (const row of hidden) expect(menu).not.toContain(row);
    },
  );

  it("leads the master to the first non-player character of the game", async () => {
    const wrapper = mountPanel([GameParticipation.Owner]);
    useGameDetailsStore().characters = [
      {
        ...createMockCharacter("c-2", "Трактирщик"),
        isNpc: asServed(true),
      },
    ];
    await nextTick();

    expect(rows(wrapper)).toContain("- Редактировать неигрового персонажа");
  });

  // A 401 drops the viewer: the master's rows leave with him, whatever roles the store holds.
  it("takes the master's rows away the moment the session ends", async () => {
    const wrapper = mountPanel([GameParticipation.Owner], "AwaitingEdits");
    expect(wrapper.text()).toContain("Управление игрой");
    expect(rows(wrapper)).toContain("- Настройки");

    // What the app does on a 401, and nothing else: no refetch follows.
    useAuthStore().updateUser(null);
    await nextTick();

    const menu = rows(wrapper);
    expect(wrapper.text()).not.toContain("Управление игрой");
    for (const row of [
      "- Настройки",
      "- Создать неигрового персонажа",
      "- Отправить на проверку",
      "- Заметки игры",
    ]) {
      expect(menu).not.toContain(row);
    }
    expect(wrapper.find("game-status-buttons-stub").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Действия с игрой");
    // The served roles are still the master's: the menu read the viewer.
    expect(useGameDetailsStore().isMaster).toBe(true);
  });

  // Three moves to two audiences; a row shown to anybody else would be answered 403 or 400.
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
      ["AwaitingApproval", "AwaitingApproval" as PremoderationStatus],
      ["Approved", "Approved" as PremoderationStatus],
      ["not sent at all", undefined],
    ])(
      "hides the submit row from the master when the status is %s",
      (_name, status) => {
        expect(
          rows(mountPanel([GameParticipation.Owner], status)),
        ).not.toContain(SUBMIT);
      },
    );

    // GameIntention.SubmitForApproval admits the master alone.
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

    // Both verdicts are legal from every status.
    it.each([
      ["Approved" as PremoderationStatus],
      ["AwaitingApproval" as PremoderationStatus],
      ["AwaitingEdits" as PremoderationStatus],
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
  });
});

/** The question before a moderation move waits for the answer and says a refusal in itself. */
describe("GamePanel moderation question", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("keeps the question open with the refusal in it", async () => {
    signedInAs(UserRole.Mentor);
    const store = useGameDetailsStore();
    const change = vi.spyOn(store, "changePremoderation").mockResolvedValue({
      type: "",
      title: "",
      status: 404,
      traceId: "t",
    });
    const wrapper = mountPanel([], "AwaitingApproval", {
      ConfirmDialog: false,
    });

    const approve = wrapper
      .findAll("button")
      .find((b) => b.text() === "Одобрить игру");
    await approve!.trigger("click");
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-submit")
      ?.click();
    await flushPromises();

    expect(change).toHaveBeenCalledWith("SetApproved", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(
      document.body.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось изменить премодерацию");
    expect(document.body.textContent).toContain("Одобрить игру?");
    wrapper.unmount();
  });
});

/** "Обсуждение" is a row for everybody who sees the game: a closed discussion closes writing, not reading. */
describe("GamePanel discussion row", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function mountWith(
    // A string: the old members-only mode is read as any other.
    commentariesAccess: CommentariesAccessMode | string,
    participation: GameParticipation[] = [],
    over: Partial<Game> = {},
  ) {
    const wrapper = mountPanel(participation);
    const store = useGameDetailsStore();
    store.game = {
      ...store.game!,
      privacySettings: {
        ...store.game!.privacySettings,
        commentariesAccess: commentariesAccess as CommentariesAccessMode,
      },
      ...over,
    };
    return wrapper;
  }

  const hasRow = (wrapper: ReturnType<typeof mountPanel>) =>
    rows(wrapper).some((row) => row.startsWith("- Обсуждение"));

  it("keeps the row for somebody outside a game sent with the old members-only mode", async () => {
    const wrapper = mountWith("Private");
    await flushPromises();

    expect(hasRow(wrapper)).toBe(true);
  });

  // Nothing is asked of the discussion to draw the row.
  it.each([
    ["somebody outside a read-only game", CommentariesAccessMode.Readonly, []],
    [
      "a player of a read-only game",
      CommentariesAccessMode.Readonly,
      [GameParticipation.Player],
    ],
    ["anybody in an open game", CommentariesAccessMode.Public, []],
  ])("keeps the row of an empty discussion for %s", async (_, mode, roles) => {
    const asked = vi.spyOn(discussionApi, "getGameComments");
    const wrapper = mountWith(mode, roles);
    await flushPromises();

    expect(hasRow(wrapper)).toBe(true);
    expect(asked).not.toHaveBeenCalled();
  });

  it("keeps the row of an empty discussion for the one on the blacklist", async () => {
    signedInAs(UserRole.RegularUser);
    const wrapper = mountWith(CommentariesAccessMode.Public, [], {
      viewerBlacklisted: true,
      viewer: { canComment: false },
    } as Partial<Game>);
    await flushPromises();

    expect(hasRow(wrapper)).toBe(true);
  });
});
