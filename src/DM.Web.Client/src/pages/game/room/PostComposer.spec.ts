/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises, config } from "@vue/test-utils";
import { GameParticipation } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import PostComposer from "./PostComposer.vue";
import { mockCreatePost } from "./room.doubles";
import {
  character,
  messages,
  refusal,
  room,
  viewerOfTheGame,
} from "./room.fixtures";

vi.mock("@/entities/game/api/roomApi", async () =>
  (await import("./room.doubles")).roomApi(),
);
vi.mock("@/entities/game/api/postApi", async () =>
  (await import("./room.doubles")).postApi(),
);

// The turns and the attachments are drawn for real.
async function mountComposer(
  formRoom: object,
  viewer: { username: string; game: object; characters: unknown[] },
  stubs: Record<string, boolean> = {},
) {
  const pinia = viewerOfTheGame(
    viewer.username,
    viewer.game,
    viewer.characters,
  );
  const wrapper = mount(PostComposer, {
    shallow: true,
    props: { room: formRoom as never, gameId: "g-1" },
    global: {
      plugins: [pinia],
      stubs: { PostPendencyField: false, AttachmentsPicker: false, ...stubs },
    },
  });
  await flushPromises();
  return wrapper;
}

const theMaster = {
  username: "master",
  game: { id: "g-1", participation: [GameParticipation.Owner] },
  characters: [character],
};

describe("PostComposer", () => {
  // Saved on the comment surface, which refuses a hidden block.
  it("checks the metagame text as the surface it is saved on", async () => {
    const wrapper = await mountComposer(room, theMaster, {
      ActionResult: false,
    });

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => [
          editor.props("context"),
          editor.props("checkContext"),
        ]),
    ).toEqual([
      ["post", undefined],
      ["post", "common"],
    ]);
  });

  it("tells a refused post in the line over the button, and in no plate", async () => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    mockCreatePost.mockResolvedValue({ data: null, error: refusal(400) });
    const wrapper = await mountComposer(room, theMaster, {
      ActionResult: false,
    });

    await wrapper
      .findAllComponents({ name: "BBCodeEditor" })[0]
      .vm.$emit("update:modelValue", "Ход");
    await wrapper.find(".composer-submit").trigger("click");
    await flushPromises();

    expect(mockCreatePost).toHaveBeenCalled();
    expect(wrapper.get(".composer .action-result").text()).toBe(
      "Не удалось отправить пост",
    );
    expect(messages()).toEqual([]);
  });

  // Stored as the author passes it: the edit gets back what was typed.
  it("sends both texts as typed, and no metagame text of spaces alone", async () => {
    mockCreatePost.mockResolvedValue({ data: null, error: refusal(400) });
    const wrapper = await mountComposer(room, theMaster, {
      ActionResult: false,
    });
    const [game, meta] = wrapper.findAllComponents({ name: "BBCodeEditor" });

    await game.vm.$emit("update:modelValue", "  [B]Ход[/B]\n");
    await meta.vm.$emit("update:modelValue", "\n вне игры ");
    await wrapper.find(".composer-submit").trigger("click");
    await flushPromises();
    expect(mockCreatePost.mock.calls.at(-1)?.[1]).toMatchObject({
      gameText: "  [B]Ход[/B]\n",
      metagameText: "\n вне игры ",
    });

    await meta.vm.$emit("update:modelValue", "  \n ");
    await wrapper.find(".composer-submit").trigger("click");
    await flushPromises();
    expect(mockCreatePost.mock.calls.at(-1)?.[1].metagameText).toBeUndefined();
  });
});

// The blacklist closes the discussion and the application, not the rooms.
describe("PostComposer on the game's blacklist", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  function mountFor(opts: { blacklisted: boolean; characterStatus: string }) {
    return mountComposer(
      { ...room, pendencies: [] },
      {
        username: "player",
        game: {
          id: "g-1",
          participation: [GameParticipation.Reader],
          viewerBlacklisted: opts.blacklisted,
        },
        characters: [{ ...character, status: opts.characterStatus }],
      },
      { SecondaryText: false },
    );
  }

  it("offers a player on the list the composer as anybody", async () => {
    const wrapper = await mountFor({
      blacklisted: true,
      characterStatus: "Active",
    });

    expect(wrapper.find(".composer").exists()).toBe(true);
  });

  it("offers no composer to a character out of play, list or not", async () => {
    const wrapper = await mountFor({
      blacklisted: false,
      characterStatus: "Retired",
    });

    expect(wrapper.find(".composer").exists()).toBe(false);
  });
});

// "От лица" offers only the characters the room lets write
// (RoomIntentionResolver.CreatePost); the master writes without one anywhere.
describe("PostComposer by the room's accesses", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const npc = {
    id: "c-npc",
    name: "Трактирщик",
    status: "Active",
    isNpc: true,
    author: null,
  };

  function mountAs(
    username: string,
    participation: GameParticipation[],
    accesses: unknown[],
  ) {
    return mountComposer(
      { ...room, pendencies: [], accesses },
      {
        username,
        game: { id: "g-1", participation },
        characters: [character, npc],
      },
    );
  }

  const full = (id: string) => ({
    id: `a-${id}`,
    policy: "Full",
    character: { id },
  });
  const readOnly = (id: string) => ({
    id: `a-${id}`,
    policy: "ReadOnly",
    character: { id },
  });
  const offered = (wrapper: Awaited<ReturnType<typeof mountAs>>) =>
    wrapper.findAllComponents({ name: "BBCodeEditor" }).length > 0;
  const postAs = (wrapper: Awaited<ReturnType<typeof mountAs>>) =>
    (
      wrapper
        .findAllComponents({ name: "Select" })
        .find((one) => one.attributes("id") === "composer-post-as")
        ?.props("options") as { label: string }[] | undefined
    )?.map((option) => option.label) ?? [];

  it("offers the composer to a player the room lets write", async () => {
    const wrapper = await mountAs(
      "player",
      [GameParticipation.Player],
      [full("c-1")],
    );

    expect(offered(wrapper)).toBe(true);
  });

  // An open room with no access of the character: open to read, not to write.
  it("offers no composer to a player whose character the room does not let in", async () => {
    const wrapper = await mountAs("player", [GameParticipation.Player], []);

    expect(offered(wrapper)).toBe(false);
    expect(wrapper.find(".composer").exists()).toBe(false);
  });

  it("offers no composer to a player whose character may only read", async () => {
    const wrapper = await mountAs(
      "player",
      [GameParticipation.Player],
      [readOnly("c-1")],
    );

    expect(offered(wrapper)).toBe(false);
  });

  it("offers the master the NPCs the room lets write, and the master's own voice", async () => {
    const withNpc = await mountAs(
      "master",
      [GameParticipation.Owner],
      [full("c-npc")],
    );
    expect(postAs(withNpc)).toEqual([
      "Трактирщик (неигровой персонаж)",
      "От лица мастера",
    ]);

    const without = await mountAs(
      "master",
      [GameParticipation.Owner],
      [readOnly("c-npc")],
    );
    expect(offered(without)).toBe(true);
    expect(postAs(without)).toEqual([]);
  });
  // A file is shown to characters as a hidden block names them: the others' players', not one's own.
  it("offers to show a file to the other characters of the room", async () => {
    const vasya = {
      id: "a-vasya",
      policy: "ReadOnly",
      character: {
        id: "c-vasya",
        name: "Вася",
        isNpc: false,
        author: { username: "vasya" },
      },
    };
    const wrapper = await mountAs(
      "player",
      [GameParticipation.Player],
      [
        { ...full("c-1"), character },
        vasya,
        { ...full("c-npc"), character: npc },
      ],
    );

    expect(
      wrapper.findComponent({ name: "AttachmentsPicker" }).props("candidates"),
    ).toEqual([{ id: "c-vasya", name: "Вася" }]);
  });
});

const HERE = dirname(fileURLToPath(import.meta.url));

// Read off the stylesheet: jsdom lays nothing out, and a fixed width cut names.
describe("PostComposer list of characters", () => {
  const rules = ruleMap(componentCss(join(HERE, "PostComposer.vue")));
  const turns = ruleMap(componentCss(join(HERE, "PostPendencyField.vue")));

  it("stands the list of 'От лица' as wide as its longest name, not wider than the column", () => {
    expect(rules.get(".composer-as .composer-as-select")).toEqual({
      width: "auto",
      "max-width": "100%",
      "min-width": "0",
    });
    // The same rule as the list beside "Ждать хода", which holds a copy of it.
    expect(rules.get(".composer-as .composer-as-select")).toEqual(
      turns.get(".pending-add .pending-select"),
    );
    expect(rules.get(".composer-as-select")).toBeUndefined();
  });

  it("puts the list under its caption where the two do not fit in a line", () => {
    expect(rules.get(".composer-as")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
    });
  });
});

// DM2's form of a post: its captions in its words and order, no frame or title.
describe("PostComposer form of a post", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const mountRoom = () =>
    mountComposer({ ...room, settings: { diceEnabled: true } }, theMaster, {
      ActionResult: false,
    });

  it("captions its fields in the glossary's words and the old site's order, with no title over them", async () => {
    config.global.renderStubDefaultSlot = true;
    const wrapper = await mountRoom().finally(() => {
      config.global.renderStubDefaultSlot = false;
    });

    const captions = wrapper
      .get(".composer")
      .findAll(".composer-label, .dice-title, .attachments-title, .turns-title")
      // A browser draws no space at the start of the button's line box.
      .map((one) => one.text().replace(/\s+/g, " ").replace("( ", "("));
    expect(captions).toEqual([
      "Игровой текст:",
      "Броски кубиков (Очистить):",
      "Метаигровой текст:",
      "Вложения:",
      "Я жду хода от:",
    ]);
    expect(wrapper.get(".composer-submit").text()).toBe("Отправить");
    // Nothing over the first caption.
    expect(wrapper.get(".composer > :first-child").text()).toBe(captions[0]);
  });

  it("puts no hint inside its fields", async () => {
    const wrapper = await mountRoom();

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => editor.props("placeholder")),
    ).toEqual(["", ""]);
  });

  it("takes every roll back by Очистить", async () => {
    const wrapper = await mountRoom();
    const dice = wrapper.findComponent({ name: "DiceComposer" });
    await dice.vm.$emit("update:modelValue", [{ dice: 20, count: 1 }]);
    expect(dice.props("modelValue")).toHaveLength(1);

    await wrapper.get(".dice-clear").trigger("click");

    expect(dice.props("modelValue")).toEqual([]);
  });

  const rules = ruleMap(componentCss(join(HERE, "PostComposer.vue")));
  const turns = ruleMap(componentCss(join(HERE, "PostPendencyField.vue")));

  it("stands on the page without a frame or a ground of its own", () => {
    expect(rules.get(".composer")).toEqual({ "margin-top": "32px" });
    expect(turns.get(".turns")).toEqual({ "margin-top": "16px" });
  });
});
