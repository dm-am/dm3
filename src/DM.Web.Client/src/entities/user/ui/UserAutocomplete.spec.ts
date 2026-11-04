/**
 * @vitest-environment jsdom
 */

// `exclude` and `includeInactive`, as the blacklists ask, an Escape that belongs to the open list,
// a list that the next keystroke opens again, and the options before a name is typed with the
// field that acts on a choice, as the messenger asks.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { defineComponent, h } from "vue";

import { IN_DIALOG } from "@/shared/ui/Layout/dialogContext";
import UserAutocomplete from "./UserAutocomplete.vue";

const searchUsers = vi.fn();

vi.mock("../api", () => ({
  userApi: {
    searchUsers: (...args: unknown[]) => searchUsers(...args),
  },
}));

const aUser = (username: string) => ({ id: `id-${username}`, username });

/** The field inside a box that hears what reaches it, as a window does. */
function mountField(
  exclude?: string[],
  inDialog = false,
  includeInactive?: boolean,
) {
  const reached: string[] = [];
  const Host = defineComponent({
    provide: { [IN_DIALOG as symbol]: inDialog },
    setup: () => () =>
      h(
        "div",
        {
          class: "host",
          onKeydown: (e: KeyboardEvent) => reached.push(e.key),
        },
        [
          h(UserAutocomplete, {
            modelValue: "",
            placeholder: "Имя пользователя",
            exclude,
            includeInactive,
          }),
        ],
      ),
  });
  const wrapper = mount(Host, { attachTo: document.body });
  return { wrapper, reached };
}

/** Types into the field the focus never left, as after a choice or an Escape, and lets the lookup answer. */
async function typeOn(wrapper: Pick<VueWrapper, "get">, text: string) {
  await wrapper.get("input").setValue(text);
  await vi.advanceTimersByTimeAsync(200);
  await flushPromises();
}

/** Focuses the field, types into it and lets the lookup answer. */
async function typeAndWait(wrapper: Pick<VueWrapper, "get">, text: string) {
  await wrapper.get("input").trigger("focus");
  await typeOn(wrapper, text);
}

const offered = (wrapper: Pick<VueWrapper, "findAll">) =>
  wrapper.findAll(".autocomplete-item .user-username").map((n) => n.text());

/** The field of the messenger: its conversations before a name, and a choice that leaves it empty. */
function mountJumpField() {
  return mount(UserAutocomplete, {
    props: {
      modelValue: "",
      initial: [
        { key: "chat-1", label: "Круглый стол" },
        { key: "chat-2", label: "Мерлин" },
      ],
      clearOnSelect: true,
    },
    attachTo: document.body,
  });
}

describe("UserAutocomplete", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    searchUsers.mockReset();
    searchUsers.mockResolvedValue({
      data: {
        resources: [aUser("Мордред"), aUser("Моргана"), aUser("Мордаунт")],
      },
      error: null,
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    document.body.innerHTML = "";
  });

  it("does not offer the names of exclude, in any case", async () => {
    const { wrapper } = mountField(["мордред", "МОРДАУНТ"]);

    await typeAndWait(wrapper, "Мор");

    expect(offered(wrapper)).toEqual(["Моргана"]);
  });

  it("offers all it found without exclude", async () => {
    const { wrapper } = mountField();

    await typeAndWait(wrapper, "Мор");

    expect(offered(wrapper)).toEqual(["Мордред", "Моргана", "Мордаунт"]);
  });

  it("keeps the Escape that closes the open list", async () => {
    const { wrapper, reached } = mountField();
    await typeAndWait(wrapper, "Мор");
    expect(offered(wrapper)).toHaveLength(3);

    await wrapper.get("input").trigger("keydown", { key: "Escape" });

    expect(wrapper.find(".autocomplete-dropdown").exists()).toBe(false);
    expect(reached).toEqual([]);
  });

  it("opens the list closed by Escape again on the next name typed", async () => {
    const { wrapper } = mountField();
    await typeAndWait(wrapper, "Мор");
    await wrapper.get("input").trigger("keydown", { key: "Escape" });

    await typeOn(wrapper, "Морд");

    expect(offered(wrapper)).toHaveLength(3);
  });

  it("lets the Escape go on when there is nothing to close", async () => {
    const { wrapper, reached } = mountField();

    await wrapper.get("input").trigger("keydown", { key: "Escape" });

    expect(reached).toEqual(["Escape"]);
  });

  it("searches among the active users without includeInactive", async () => {
    const { wrapper } = mountField();

    await typeAndWait(wrapper, "Мор");

    expect(searchUsers).toHaveBeenCalledWith("Мор", 12, undefined);
  });

  it("searches among everyone with includeInactive, the long absent too", async () => {
    const { wrapper } = mountField(undefined, false, true);

    await typeAndWait(wrapper, "Мор");

    expect(searchUsers).toHaveBeenCalledWith("Мор", 12, "All");
  });

  it("offers the caller's options while nothing is typed and hands back the key of the one picked", async () => {
    const wrapper = mountJumpField();

    await wrapper.get("input").trigger("focus");
    expect(offered(wrapper)).toEqual(["Круглый стол", "Мерлин"]);
    await wrapper.findAll(".autocomplete-item")[1].trigger("mousedown");

    expect(wrapper.emitted("pick")).toEqual([["chat-2"]]);
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    expect(wrapper.find(".autocomplete-dropdown").exists()).toBe(false);
  });

  it("puts the people found in place of the options once a name is typed", async () => {
    const wrapper = mountJumpField();

    await typeAndWait(wrapper, "Мор");

    expect(offered(wrapper)).toEqual(["Мордред", "Моргана", "Мордаунт"]);
  });

  it("hands out the name chosen and stays an empty field that opens again on the next name", async () => {
    const wrapper = mountJumpField();
    await typeAndWait(wrapper, "Мор");

    await wrapper.findAll(".autocomplete-item")[1].trigger("mousedown");

    expect(wrapper.emitted("update:modelValue")).toEqual([["Моргана"]]);
    expect(wrapper.find(".selected-user").exists()).toBe(false);
    expect((wrapper.get("input").element as HTMLInputElement).value).toBe("");
    await typeOn(wrapper, "Мор");
    expect(offered(wrapper)).toHaveLength(3);
  });

  it("takes the height of the window's fields in a window", () => {
    const { wrapper } = mountField(undefined, true);

    expect(wrapper.get(".user-autocomplete").classes()).toContain(
      "user-autocomplete--dialog",
    );
  });
});
