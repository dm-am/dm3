/**
 * @vitest-environment jsdom
 */

/**
 * The rooms of a game, managed the way the old site manages them: the form of
 * a new room on top ("Название", "Тип", "Доступ: Закрытая / Открытая",
 * "Создать"), and one table of every room under it, edited in its cells and
 * saved by one button.
 *
 * What these catch, since a table that sends nothing looks perfectly normal on
 * screen:
 *  - a cell that edits a copy and not the draft the save reads;
 *  - a save that sends rooms nothing changed in, or the whole table at once;
 *  - a grant ticked or unticked that never reaches the server, or reaches it
 *    with a policy the server reads as no access at all;
 *  - "Сохранено" over a save that was refused, or kept after a change;
 *  - a room created on the way taking back what was typed in the others;
 *  - a place typed in "Порядок" that moves no room, or moves rooms nobody
 *    renumbered.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import {
  gameApi,
  useGameDetailsStore,
  RoomAccessPolicy,
  RoomAccessType,
  RoomType,
  type Character,
  type Game,
  type Room,
} from "@/entities/game";
import { UserAutocomplete } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ruleMap, scopedComponentCss } from "@/shared/lib/testing/styles";
import RoomsSection from "./RoomsSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "t",
});

/** A room as the listing sends it, open and settled. */
function aRoom(id: string, title: string, extra: Partial<Room> = {}): Room {
  return {
    id,
    roomNumber: Number(id.replace(/\D/g, "")) || 1,
    title,
    type: RoomType.Default,
    access: RoomAccessType.Open,
    isArchived: false,
    unreadPostsCount: 0,
    accesses: [],
    settings: {
      viewPrivateText: false,
      viewDiceResults: false,
      diceEnabled: false,
      hiddenWithoutAccess: false,
    },
    ...extra,
  } as unknown as Room;
}

const aCharacter = (id: string, name: string) =>
  ({ id, name, status: "Active" }) as unknown as Character;

const ELF = aCharacter("c-1", "Эльвира");
const RANGER = aCharacter("c-2", "Арагорн");

/** The rooms of a game, open in front of its master. */
function mountSection(rooms: Room[], characters: Character[] = []) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Игра",
  } as unknown as Game;
  store.rooms = rooms;
  store.characters = characters;
  // The reads this section fires are not this spec's subject; left real they
  // would reach for the endpoints.
  vi.spyOn(store, "loadRooms").mockResolvedValue(undefined);
  vi.spyOn(store, "loadCharacters").mockResolvedValue(undefined);

  // The confirmation of a delete is the real window, drawn in place.
  return mount(RoomsSection, {
    global: { plugins: [pinia], stubs: { Teleport: true } },
  });
}

/** The two rows of a room, by its number. */
const roomOf = (wrapper: VueWrapper, number: number) =>
  wrapper.get(`tbody[data-room='${number}']`);

const saveButton = (wrapper: VueWrapper) => wrapper.get(".save-all");

/** What the button says, and whether it can still be pressed. */
const answer = (wrapper: VueWrapper) => ({
  label: saveButton(wrapper).text(),
  inert: saveButton(wrapper).attributes("disabled") !== undefined,
});

async function save(wrapper: VueWrapper) {
  await saveButton(wrapper).trigger("click");
  await flushPromises();
}

/** The box of a character in "Игроки" of a room. */
const writerBox = (wrapper: VueWrapper, number: number, name: string) =>
  roomOf(wrapper, number)
    .findAll(".col-writers label")
    .find((label) => label.text() === name)!
    .get("input");

let update: ReturnType<typeof vi.fn>;
let grant: ReturnType<typeof vi.fn>;
let revoke: ReturnType<typeof vi.fn>;

beforeEach(() => {
  [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  update = vi.fn().mockResolvedValue({ data: null, error: null });
  grant = vi.fn().mockResolvedValue({ data: null, error: null });
  revoke = vi.fn().mockResolvedValue({ data: null, error: null });
  vi.spyOn(gameApi, "updateRoom").mockImplementation(update as never);
  vi.spyOn(gameApi, "createRoomAccess").mockImplementation(grant as never);
  vi.spyOn(gameApi, "deleteRoomAccess").mockImplementation(revoke as never);
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("RoomsSection table", () => {
  it("has the old site's columns and a row for every room, archived ones in their place", () => {
    const wrapper = mountSection([
      aRoom("r1", "Таверна", { totalPostsCount: 12 }),
      aRoom("r2", "Пролог", { isArchived: true }),
      aRoom("r3", "Кулуары", { type: RoomType.Chat, totalPostsCount: 5 }),
    ]);

    expect(
      wrapper.findAll(".rooms-table thead th").map((th) => th.text()),
    ).toEqual([
      "#",
      "Название",
      "Тип",
      "Доступ",
      "Порядок",
      "Сообщений",
      "Действия",
    ]);
    expect(
      wrapper
        .findAll("tbody.room")
        .map((room) => [
          room.get(".col-number").text(),
          (room.get(".col-title input").element as HTMLInputElement).value,
          room.get(".col-type").text(),
          (room.get(".col-order input").element as HTMLInputElement).value,
          room.get(".col-messages").text(),
        ]),
    ).toEqual([
      ["1", "Таверна", "Посты", "1", "12"],
      ["2", "Пролог", "Посты", "2", "0"],
      ["3", "Кулуары", "Чат", "3", "5"],
    ]);
    const activity = roomOf(wrapper, 2)
      .findAllComponents(Select)
      .map((select: VueWrapper<InstanceType<typeof Select>>) =>
        select.props("modelValue"),
      );
    expect(activity).toEqual([RoomAccessType.Open, "archived"]);
    // The activity stands under the lists of the room, captioned as they are.
    expect(
      roomOf(wrapper, 2)
        .findAll(".col-details .grants-caption")
        .map((caption) => caption.text()),
    ).toContain("Активность:");
    expect(roomOf(wrapper, 2).find(".col-activity select").exists()).toBe(true);
  });

  it("lists every character in play under a room, the ones that write in it ticked", () => {
    const wrapper = mountSection(
      [
        aRoom("r1", "Таверна", {
          accesses: [
            {
              id: "a-1",
              policy: RoomAccessPolicy.Full,
              character: { id: "c-2", name: "Арагорн" },
            },
          ] as never,
        }),
      ],
      [ELF, RANGER],
    );

    const boxes = roomOf(wrapper, 1).findAll(".col-writers label");
    expect(boxes.map((label) => label.text())).toEqual(["Эльвира", "Арагорн"]);
    expect(
      boxes.map(
        (label) => (label.get("input").element as HTMLInputElement).checked,
      ),
    ).toEqual([false, true]);
  });

  it("offers a reader to a closed room and to an open chat, and not to an open room of posts", () => {
    const wrapper = mountSection([
      aRoom("r1", "Таверна"),
      aRoom("r2", "Склеп", { access: RoomAccessType.Private }),
      aRoom("r3", "Кулуары", { type: RoomType.Chat }),
    ]);

    // The field stands in the row of what it gives: reading a closed room,
    // writing in an open chat.
    const offers = (number: number) =>
      ["col-writers", "col-readers"].filter((row) =>
        roomOf(wrapper, number)
          .findAll(`.${row}`)
          .some((list) => list.findComponent(UserAutocomplete).exists()),
      );
    expect([offers(1), offers(2), offers(3)]).toEqual([
      [],
      ["col-readers"],
      ["col-writers"],
    ]);
    expect(
      roomOf(wrapper, 3).findComponent(UserAutocomplete).props("placeholder"),
    ).toBe("Разрешить писать в чате");
  });

  // A person let into a chat with "Full" writes in it, and one let into a room
  // of posts reads it whatever the policy: a post is written as a character.
  it("lists a person let in by name in the row of what his grant lets him do", () => {
    const people = [
      {
        id: "a-1",
        policy: RoomAccessPolicy.Full,
        user: { username: "Writer" },
      },
      {
        id: "a-2",
        policy: RoomAccessPolicy.ReadOnly,
        user: { username: "Reader" },
      },
    ];
    const wrapper = mountSection([
      aRoom("r1", "Кулуары", {
        type: RoomType.Chat,
        access: RoomAccessType.Private,
        accesses: people as never,
      }),
      aRoom("r2", "Склеп", {
        access: RoomAccessType.Private,
        accesses: people as never,
      }),
    ]);

    const names = (number: number, row: string) =>
      roomOf(wrapper, number)
        .findAll(`.${row} label`)
        .map((label) => label.text());
    expect(names(1, "col-writers")).toEqual(["Writer"]);
    expect(names(1, "col-readers")).toEqual(["Reader"]);
    expect(names(2, "col-writers")).toEqual([]);
    expect(names(2, "col-readers")).toEqual(["Writer", "Reader"]);
  });

  it("names the old site's settings of a room, the hiding one for a closed room alone", () => {
    const wrapper = mountSection([
      aRoom("r1", "Таверна"),
      aRoom("r2", "Склеп", { access: RoomAccessType.Private }),
    ]);

    const settings = (number: number) =>
      roomOf(wrapper, number)
        .findAll(".room-settings label")
        .map((label) => label.text());
    expect(settings(1)).toEqual([
      "Поддержка кубика",
      "Показывать всем результаты бросков",
      "Показывать всем приватные сообщения",
    ]);
    expect(settings(2)).toContain("Скрывать от тех, у кого нет доступа");
  });
});

describe("RoomsSection save", () => {
  it("does not claim a save the reader has not made", () => {
    const wrapper = mountSection([aRoom("r1", "Таверна")]);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("sends a room what changed in it and nothing to the rooms left as they were", async () => {
    const wrapper = mountSection([
      aRoom("r1", "Таверна"),
      aRoom("r2", "Пролог"),
    ]);

    await roomOf(wrapper, 2).get(".col-title input").setValue("Эпилог");
    await roomOf(wrapper, 2)
      .findAllComponents(Select)[1]
      .vm.$emit("update:modelValue", "archived");
    await roomOf(wrapper, 2).findAll(".room-settings input")[0].setValue(true);
    await save(wrapper);

    expect(update).toHaveBeenCalledTimes(1);
    expect(update).toHaveBeenCalledWith("r2", {
      title: "Эпилог",
      access: RoomAccessType.Open,
      isArchived: true,
      settings: {
        viewPrivateText: false,
        viewDiceResults: false,
        diceEnabled: true,
        hiddenWithoutAccess: false,
      },
    });
    expect(grant).not.toHaveBeenCalled();
    expect(revoke).not.toHaveBeenCalled();
  });

  it("closes a room by its list of access", async () => {
    const wrapper = mountSection([aRoom("r1", "Таверна")]);

    await roomOf(wrapper, 1)
      .findAllComponents(Select)[0]
      .vm.$emit("update:modelValue", RoomAccessType.Private);
    await save(wrapper);

    expect(update).toHaveBeenCalledWith(
      "r1",
      expect.objectContaining({ access: RoomAccessType.Private }),
    );
  });

  it("lets a character write by its box, and takes the grant off by the box", async () => {
    const wrapper = mountSection(
      [
        aRoom("r1", "Таверна", {
          accesses: [
            {
              id: "a-1",
              policy: RoomAccessPolicy.Full,
              character: { id: "c-2", name: "Арагорн" },
            },
          ] as never,
        }),
      ],
      [ELF, RANGER],
    );

    await writerBox(wrapper, 1, "Эльвира").setValue(true);
    await writerBox(wrapper, 1, "Арагорн").setValue(false);
    await save(wrapper);

    expect(update).not.toHaveBeenCalled();
    expect(revoke).toHaveBeenCalledWith("a-1");
    expect(grant).toHaveBeenCalledWith("r1", {
      character: { id: "c-1" },
      policy: RoomAccessPolicy.Full,
    });
  });

  it("seats a reader of a closed room as a reader, and one of an open chat as a writer", async () => {
    const wrapper = mountSection([
      aRoom("r1", "Склеп", { access: RoomAccessType.Private }),
      aRoom("r2", "Кулуары", { type: RoomType.Chat }),
    ]);

    roomOf(wrapper, 1)
      .findComponent(UserAutocomplete)
      .vm.$emit("update:modelValue", "Reader1");
    roomOf(wrapper, 2)
      .findComponent(UserAutocomplete)
      .vm.$emit("update:modelValue", "Reader2");
    await flushPromises();

    expect(
      roomOf(wrapper, 1)
        .findAll(".col-readers label")
        .map((label) => label.text()),
    ).toEqual(["Reader1"]);
    // In the open chat the person writes, and stands with those who do.
    expect(
      roomOf(wrapper, 2)
        .findAll(".col-writers label")
        .map((label) => label.text()),
    ).toEqual(["Reader2"]);
    expect(roomOf(wrapper, 2).find(".col-readers").exists()).toBe(false);
    // The field empties once it has taken the name.
    expect(
      roomOf(wrapper, 1).findComponent(UserAutocomplete).props("modelValue"),
    ).toBe("");

    await save(wrapper);

    expect(grant).toHaveBeenCalledWith("r1", {
      user: { username: "Reader1" },
      policy: RoomAccessPolicy.ReadOnly,
    });
    expect(grant).toHaveBeenCalledWith("r2", {
      user: { username: "Reader2" },
      policy: RoomAccessPolicy.Full,
    });
  });

  it("takes a reader off by his box", async () => {
    const wrapper = mountSection([
      aRoom("r1", "Склеп", {
        access: RoomAccessType.Private,
        accesses: [
          {
            id: "a-7",
            policy: RoomAccessPolicy.ReadOnly,
            user: { username: "Reader1" },
          },
        ] as never,
      }),
    ]);

    await roomOf(wrapper, 1).get(".col-readers input").setValue(false);
    await save(wrapper);

    expect(revoke).toHaveBeenCalledWith("a-7");
    expect(grant).not.toHaveBeenCalled();
  });

  it("moves a character let in to read over to writing: the old grant off first", async () => {
    const wrapper = mountSection(
      [
        aRoom("r1", "Склеп", {
          access: RoomAccessType.Private,
          accesses: [
            {
              id: "a-3",
              policy: RoomAccessPolicy.ReadOnly,
              character: { id: "c-1", name: "Эльвира" },
            },
          ] as never,
        }),
      ],
      [ELF],
    );
    expect(
      roomOf(wrapper, 1)
        .findAll(".col-readers label")
        .map((label) => label.text()),
    ).toEqual(["Эльвира"]);

    await writerBox(wrapper, 1, "Эльвира").setValue(true);
    // A character that writes is not listed among the readers as well.
    expect(roomOf(wrapper, 1).findAll(".col-readers label")).toHaveLength(0);
    await save(wrapper);

    expect(revoke).toHaveBeenCalledWith("a-3");
    expect(grant).toHaveBeenCalledWith("r1", {
      character: { id: "c-1" },
      policy: RoomAccessPolicy.Full,
    });
    expect(revoke.mock.invocationCallOrder[0]).toBeLessThan(
      grant.mock.invocationCallOrder[0],
    );
  });

  it("reads Сохранено once the server confirms, and Сохранить again after a change", async () => {
    const wrapper = mountSection([aRoom("r1", "Таверна")]);

    await roomOf(wrapper, 1).get(".col-title input").setValue("Трактир");
    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
    expect(saidOutLoud()).toEqual([]);

    await roomOf(wrapper, 1).findAll(".room-settings input")[1].setValue(true);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("names the refused room under the table, keeps offering the save and keeps the edits", async () => {
    update.mockResolvedValue({ data: null, error: refusal(400) });
    const wrapper = mountSection([aRoom("r1", "Таверна")]);

    await roomOf(wrapper, 1).get(".col-title input").setValue("Трактир");
    await save(wrapper);

    expect(wrapper.get(".settings-section > .action-result").text()).toBe(
      'Не удалось сохранить комнату "Таверна"',
    );
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    expect(
      (roomOf(wrapper, 1).get(".col-title input").element as HTMLInputElement)
        .value,
    ).toBe("Трактир");
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps what was typed in the rooms when a new room comes into the list", async () => {
    const wrapper = mountSection([aRoom("r1", "Таверна")]);
    await roomOf(wrapper, 1).get(".col-title input").setValue("Трактир");

    useGameDetailsStore().rooms = [
      aRoom("r1", "Таверна"),
      aRoom("r2", "Подвал"),
    ];
    await nextTick();

    expect(
      (roomOf(wrapper, 1).get(".col-title input").element as HTMLInputElement)
        .value,
    ).toBe("Трактир");
    expect(
      (roomOf(wrapper, 2).get(".col-title input").element as HTMLInputElement)
        .value,
    ).toBe("Подвал");
  });
});

/**
 * "Порядок" is the old site's field of a number: the place a room stands at,
 * counted from one, typed over to move it. The server takes a move as "put
 * this room right after that one", so a save sends one such move for every
 * room renumbered and none for the rest.
 */
describe("RoomsSection order", () => {
  const three = () =>
    mountSection([
      aRoom("r1", "Таверна"),
      aRoom("r2", "Подвал"),
      aRoom("r3", "Чердак"),
    ]);
  const typePlace = (wrapper: VueWrapper, number: number, place: string) =>
    roomOf(wrapper, number).get(".col-order input").setValue(place);
  const moves = () =>
    update.mock.calls.filter(([, body]) => "previousRoomId" in body);

  it("puts a room first with one move", async () => {
    const wrapper = three();
    await typePlace(wrapper, 3, "1");
    await save(wrapper);

    expect(update.mock.calls).toEqual([["r3", { previousRoomId: null }]]);
  });

  it("puts a room down after the room that is to stand before it", async () => {
    const wrapper = three();
    await typePlace(wrapper, 1, "3");
    await save(wrapper);

    expect(update.mock.calls).toEqual([["r1", { previousRoomId: "r3" }]]);
  });

  it("swaps two rooms with one move", async () => {
    const wrapper = three();
    await typePlace(wrapper, 1, "2");
    await typePlace(wrapper, 2, "1");
    await save(wrapper);

    expect(update.mock.calls).toEqual([["r2", { previousRoomId: null }]]);
  });

  it("reads a place past the end as the end, and what is no place as none", async () => {
    const wrapper = three();
    await typePlace(wrapper, 1, "9");
    await typePlace(wrapper, 2, "-");
    await save(wrapper);

    expect(moves()).toEqual([["r1", { previousRoomId: "r3" }]]);
  });

  it("moves no room while every room keeps its place", async () => {
    const wrapper = three();
    await typePlace(wrapper, 2, "2");
    await roomOf(wrapper, 2).get(".col-title input").setValue("Погреб");
    await save(wrapper);

    expect(moves()).toEqual([]);
    expect(update).toHaveBeenCalledTimes(1);
  });

  it("names the room whose move was refused and stops there", async () => {
    update.mockResolvedValueOnce({ data: null, error: refusal(400) });
    const wrapper = three();
    await typePlace(wrapper, 1, "3");
    await save(wrapper);

    expect(wrapper.get(".settings-section > .action-result").text()).toBe(
      'Не удалось сохранить комнату "Таверна"',
    );
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    expect(
      (roomOf(wrapper, 1).get(".col-order input").element as HTMLInputElement)
        .value,
    ).toBe("3");
  });
});

describe("RoomsSection new room", () => {
  it("starts a room closed, as the old site does, and sends what the form holds", async () => {
    const wrapper = mountSection([]);
    const create = vi
      .spyOn(useGameDetailsStore(), "createRoom")
      .mockResolvedValue(null);

    expect(
      (
        wrapper.get("input[name='new-room-access'][value='Private']")
          .element as HTMLInputElement
      ).checked,
    ).toBe(true);

    await wrapper.get("#new-room-title").setValue("  Склеп  ");
    await wrapper
      .get("input[name='new-room-type'][value='Chat']")
      .setValue(true);
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(create).toHaveBeenCalledWith({
      title: "Склеп",
      type: RoomType.Chat,
      accessType: RoomAccessType.Private,
      viewPrivateText: false,
      viewDiceResults: false,
      diceEnabled: false,
    });
    expect(
      (wrapper.get("#new-room-title").element as HTMLInputElement).value,
    ).toBe("");
  });

  it("has no table to save while the game has no rooms", () => {
    const wrapper = mountSection([]);

    expect(wrapper.text()).toContain("Комнат пока нет");
    expect(wrapper.find(".save-all").exists()).toBe(false);
  });
});

/**
 * Deleting a room. The confirmation runs the request itself: it waits for the
 * answer, prints a refusal in itself and closes on success alone, and the
 * request carries the flags that keep the interceptor quiet about it.
 */
describe("RoomsSection delete", () => {
  async function deleteRoom(reply: unknown) {
    const request = vi
      .spyOn(gameApi, "deleteRoom")
      .mockResolvedValue(reply as never);
    const wrapper = mountSection([aRoom("r1", "Таверна")]);
    await roomOf(wrapper, 1).get(".col-actions button").trigger("click");
    await wrapper.get(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, request };
  }

  it("deletes the room with the window's flags and closes", async () => {
    const { wrapper, request } = await deleteRoom({ data: null, error: null });

    expect(request).toHaveBeenCalledWith("r1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(useGameDetailsStore().loadRooms).toHaveBeenCalledWith("the-game");
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused delete in the window and stays open", async () => {
    const { wrapper } = await deleteRoom({ data: null, error: refusal(400) });

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить комнату",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});

/**
 * The rows of the form of a new room are laid out as the other forms of a
 * game are (+form-row): the caption as tall as a field, so it stands level
 * with the field beside it. The rule has to reach the caption inside the
 * FormField, and a selector that did not left every caption at the top of
 * its row, over the middle of the radios. Read off the stylesheet as the
 * build scopes it: jsdom lays nothing out.
 */
describe("RoomsSection new room look", () => {
  const HERE = dirname(fileURLToPath(import.meta.url));
  const captionOf = (file: string, scope: string) =>
    [...ruleMap(scopedComponentCss(join(HERE, file), scope))].find(
      ([selector]) => selector.endsWith(`.row[${scope}] > .form-field-label`),
    )?.[1];

  it("stands a caption level with its field, as the settings of the game do", () => {
    const rooms = captionOf("RoomsSection.vue", "data-v-rooms");
    expect(rooms, "no rule reaches the caption of a row").toBeDefined();
    expect(rooms).toEqual(captionOf("GameInfoSection.vue", "data-v-info"));
    expect(rooms).toHaveProperty("min-height");
  });

  // The lists of every room start at one line of the table: the column of the
  // captions does not follow the captions a room happens to have, and
  // "Активность:" (73 at the caption's size, the longest) fits in it.
  it("starts the lists of every room at one line", () => {
    const grants = [
      ...ruleMap(
        scopedComponentCss(join(HERE, "RoomsSection.vue"), "data-v-rooms"),
      ),
    ].find(([selector]) => selector === ".grants[data-v-rooms]")?.[1];
    expect(grants?.["grid-template-columns"]).toBe(
      "minmax(76px, auto) minmax(0, 1fr)",
    );
  });
});
