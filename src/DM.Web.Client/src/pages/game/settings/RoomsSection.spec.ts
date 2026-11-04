/**
 * @vitest-environment jsdom
 */

// The table of rooms, edited in its cells and sent by one button: a table that
// sends nothing looks perfectly normal on screen.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import {
  roomApi,
  useGameDetailsStore,
  RoomAccessPolicy,
  RoomAccessType,
  RoomType,
} from "@/entities/game";
import { UserAutocomplete } from "@/entities/user";
import { Select } from "@/shared/ui/Select";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { ruleMap, scopedComponentCss } from "@/shared/lib/testing/styles";
import {
  ELF,
  RANGER,
  aRoom,
  answer,
  dismissPlates,
  mountSection,
  refusal,
  roomOf,
  saidOutLoud,
  save,
  writerBox,
} from "./RoomsSection.fixtures";

let update: ReturnType<typeof vi.fn>;
let grant: ReturnType<typeof vi.fn>;
let revoke: ReturnType<typeof vi.fn>;

beforeEach(() => {
  dismissPlates();
  update = vi.fn().mockResolvedValue({ data: null, error: null });
  grant = vi.fn().mockResolvedValue({ data: null, error: null });
  revoke = vi.fn().mockResolvedValue({ data: null, error: null });
  vi.spyOn(roomApi, "updateRoom").mockImplementation(update as never);
  vi.spyOn(roomApi, "createRoomAccess").mockImplementation(grant as never);
  vi.spyOn(roomApi, "deleteRoomAccess").mockImplementation(revoke as never);
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
    expect(
      roomOf(wrapper, 2)
        .findAll(".col-details .grants-caption")
        .map((caption) => caption.text()),
    ).toContain("Активность:");
    expect(roomOf(wrapper, 2).find(".col-activity select").exists()).toBe(true);
  });

  // RoomFieldLimits.TitleMaxLength, as the field of a new room.
  it("stops the title of a room at the hundred the server keeps", () => {
    const wrapper = mountSection([aRoom("r1", "Таверна")]);

    expect(
      (roomOf(wrapper, 1).get(".col-title input").element as HTMLInputElement)
        .maxLength,
    ).toBe(100);
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

    // In the row of what it gives: reading a closed room, writing in a chat.
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

  // A post is written as a character, so a person let into posts only reads.
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
      "Показывать всем скрытые блоки",
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
    expect(
      roomOf(wrapper, 2)
        .findAll(".col-writers label")
        .map((label) => label.text()),
    ).toEqual(["Reader2"]);
    expect(roomOf(wrapper, 2).find(".col-readers").exists()).toBe(false);
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

// One move ("after that room") for every room renumbered, none for the rest.
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

// The confirmation runs the request and prints its own refusal.
describe("RoomsSection delete", () => {
  async function deleteRoom(reply: unknown) {
    const request = vi
      .spyOn(roomApi, "deleteRoom")
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
    expect(useGameDetailsStore().loadRooms).not.toHaveBeenCalled();
    expect(saidOutLoud()).toEqual([]);
  });
});

describe("RoomsSection look", () => {
  const HERE = dirname(fileURLToPath(import.meta.url));

  // Off the stylesheet: jsdom lays nothing out.
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
