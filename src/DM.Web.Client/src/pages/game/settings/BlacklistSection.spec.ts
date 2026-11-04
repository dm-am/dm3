/**
 * @vitest-environment jsdom
 */

/**
 * What the game's section hands the shared BlacklistEditor: the list read by
 * the public id, the block posted by the guid.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { gameApi, useGameDetailsStore, type Game } from "@/entities/game";
import { UserAutocomplete } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { User } from "@/shared/api/models/common";
import BlacklistSection from "./BlacklistSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const aUser = (username: string): User =>
  ({ id: `id-${username}`, username }) as unknown as User;

const LANCELOT = aUser("Ланселот");
const MORDRED = aUser("Мордред");

function pending<T>() {
  let settle: (value: T) => void = () => {};
  const promise = new Promise<T>((resolve) => {
    settle = resolve;
  });
  return { promise, answer: (value: T) => settle(value) };
}

const listOf = (users: User[]) =>
  ({ data: { resources: users }, error: null }) as never;

// The second read of the list is held back by the test.
async function mountSection(first: unknown = listOf([LANCELOT])) {
  setActivePinia(createPinia());
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Хроники Амбера",
    master: { username: "Мерлин" },
    mentor: null,
    assistants: [{ username: "Кей" }],
    players: [{ username: "Гавейн" }],
  } as unknown as Game;

  const reload = pending<unknown>();
  vi.spyOn(gameApi, "getBlacklist")
    .mockResolvedValueOnce(first as never)
    .mockImplementationOnce(() => reload.promise as never);

  const wrapper = mount(BlacklistSection, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return { wrapper, reload };
}

type Section = Awaited<ReturnType<typeof mountSection>>["wrapper"];

const namesOnScreen = (wrapper: Section) =>
  wrapper
    .findAll("tbody tr.table-row .col-user .user-link")
    .map((link) => link.text());

const field = (wrapper: Section) => wrapper.getComponent(UserAutocomplete);

async function addByField(wrapper: Section, username: string) {
  field(wrapper).vm.$emit("update:modelValue", username);
  await flushPromises();
  const button = wrapper.findAll("button").find((b) => b.text() === "Добавить");
  await button!.trigger("click");
  await flushPromises();
}

describe("BlacklistSection of a game", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => vi.restoreAllMocks());

  it("keeps the field until the list shows the new name", async () => {
    const { wrapper, reload } = await mountSection();
    const add = vi.spyOn(gameApi, "addToBlacklist").mockResolvedValue({
      data: MORDRED,
      error: null,
    } as never);

    await addByField(wrapper, "Мордред");

    expect(add).toHaveBeenCalledWith("the-game-guid", "Мордред");
    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(field(wrapper).props("modelValue")).toBe("Мордред");

    reload.answer(listOf([LANCELOT, MORDRED]));
    await flushPromises();

    expect(namesOnScreen(wrapper)).toEqual(["Ланселот", "Мордред"]);
    expect(field(wrapper).props("modelValue")).toBe("");
    expect(gameApi.getBlacklist).toHaveBeenLastCalledWith("the-game");
    expect(saidOutLoud()).toEqual([]);
  });

  it("says a refusal under the field and leaves the list alone", async () => {
    const { wrapper } = await mountSection();
    vi.spyOn(gameApi, "addToBlacklist").mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Сначала уберите пользователя из игры",
        status: 409,
        traceId: "t",
      },
    } as never);

    await addByField(wrapper, "Гавейн");

    expect(wrapper.get(".bl-add .action-result").text()).toBe(
      "Сначала уберите пользователя из игры",
    );
    expect(field(wrapper).props("modelValue")).toBe("Гавейн");
    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(gameApi.getBlacklist).toHaveBeenCalledTimes(1);
    expect(saidOutLoud()).toEqual([]);
  });

  it("does not suggest the people of the game in the field", async () => {
    const { wrapper } = await mountSection();

    expect([...(field(wrapper).props("exclude") as string[])]).toEqual(
      expect.arrayContaining(["Мерлин", "Кей", "Гавейн", "Ланселот"]),
    );
  });

  it("keeps a name whose removal was refused and says so under it", async () => {
    const { wrapper } = await mountSection();
    vi.spyOn(gameApi, "removeFromBlacklist").mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "t" },
    } as never);

    const remove = wrapper.get("tbody tr.table-row .col-action button");
    expect(remove.attributes("aria-label")).toBe(
      "Удалить Ланселот из черного списка",
    );
    await remove.trigger("click");
    await flushPromises();

    expect(namesOnScreen(wrapper)).toEqual(["Ланселот"]);
    expect(wrapper.get(".table-details-row .action-result").text()).toBe(
      "Пользователь не удален из черного списка",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  it("says a failed load, not an empty list", async () => {
    const { wrapper } = await mountSection({
      data: null,
      error: { status: 500, title: "", type: "", traceId: "t" },
    });

    expect(wrapper.text()).toContain("Не удалось загрузить черный список");
    expect(wrapper.text()).not.toContain("В черном списке пока никого нет");
  });

  // The editor is shared, the line is the section's.
  it("says under the heading what the list does", async () => {
    const { wrapper } = await mountSection();

    expect(wrapper.get(".settings-section h2").text()).toBe("Черный список");
    expect(wrapper.get(".bl-hint").text()).toBe(
      "Внесенные не пишут в обсуждении игры и не подают заявки.",
    );
  });
});
