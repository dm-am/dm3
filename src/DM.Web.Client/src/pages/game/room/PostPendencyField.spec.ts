/**
 * @vitest-environment jsdom
 */

// A refused change of the turns is said under the block, with no plate, and
// the room is read again only when the server agreed.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { GameParticipation } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import PostPendencyField from "./PostPendencyField.vue";
import {
  mockCreatePendency,
  mockDeletePendency,
  mockGetRooms,
} from "./room.doubles";
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

/** The field of the master's form, for the room and the characters given. */
async function mountField(fieldRoom: object, characters: unknown[]) {
  const pinia = viewerOfTheGame(
    "master",
    { id: "g-1", participation: [GameParticipation.Owner] },
    characters,
  );

  const wrapper = mount(PostPendencyField, {
    shallow: true,
    props: { room: fieldRoom as never, gameId: "g-1" },
    global: { plugins: [pinia], stubs: { ActionResult: false } },
  });
  await flushPromises();
  return wrapper;
}

const mountRoom = () => mountField(room, [character]);

describe("PostPendencyField", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    vi.clearAllMocks();
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    mockCreatePendency.mockResolvedValue({ data: null, error: null });
    mockDeletePendency.mockResolvedValue({ data: null, error: null });
  });

  it("says why the character could not be added to the queue", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(wrapper.get(".turns .action-result").text()).toBe(
      "Не удалось добавить ожидание хода",
    );
    expect(messages()).toEqual([]);
  });

  it("says why the awaited turn could not be dropped", async () => {
    mockDeletePendency.mockResolvedValue({ data: null, error: refusal(404) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-dismiss").trigger("click");
    await flushPromises();

    expect(wrapper.get(".turns .action-result").text()).toBe(
      "Не удалось снять ожидание хода",
    );
    expect(messages()).toEqual([]);
  });

  it("refreshes the room and stays quiet when the server agreed", async () => {
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before + 1);
    expect(messages()).toEqual([]);
  });

  it("does not refresh the room over a refused pendency", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before);
  });
});

// "Ждать хода" offers the characters the server takes and no others.
describe("PostPendencyField offers", () => {
  const npc = {
    id: "c-2",
    name: "Трактирщик",
    status: "Active",
    isNpc: true,
    author: null,
  };
  const outsider = {
    id: "c-3",
    name: "Странник",
    status: "Active",
    isNpc: false,
    author: { username: "other" },
  };
  const retired = {
    id: "c-4",
    name: "Ветеран",
    status: "Retired",
    isNpc: false,
    author: { username: "old" },
  };
  const admitted = (...ids: string[]) =>
    ids.map((id, i) => ({ id: `a-${i}`, policy: "Full", character: { id } }));

  const mountWith = (accesses: unknown[]) =>
    mountField({ ...room, accesses }, [character, npc, outsider, retired]);

  const offered = (wrapper: Awaited<ReturnType<typeof mountRoom>>) =>
    (
      wrapper
        .findAllComponents({ name: "Select" })
        .find((select) => select.classes("pending-select"))
        ?.props("options") as { label: string }[] | undefined
    )?.map((option) => option.label);

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("offers only a character in play with a player and an access to the room", async () => {
    const wrapper = await mountWith(admitted("c-1", "c-2", "c-4"));

    expect(offered(wrapper)).toEqual(["Гоблин"]);
  });

  it("has no control to add a turn where nobody can be waited for", async () => {
    const wrapper = await mountWith([]);

    expect(wrapper.find(".pending-add").exists()).toBe(false);
    expect(wrapper.find(".pending-dismiss").exists()).toBe(true);
  });
});
