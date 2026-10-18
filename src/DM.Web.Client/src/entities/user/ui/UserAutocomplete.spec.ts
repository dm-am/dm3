/**
 * @vitest-environment jsdom
 */

/**
 * The name field with suggestions, in the two things the blacklists ask of it.
 *
 * It does not offer the names the caller leaves out (`exclude`): the window of
 * a blacklist would otherwise suggest the viewer, the names on the list and
 * the people who run the game, and the server refuses every one of them after
 * the press. And an Escape that closes the open list is the list's: in a
 * window it used to go on to the window, which took it for "close" and shut
 * over the half-typed name.
 *
 * The blacklists ask one more thing of it: to look among everyone, the people
 * who have not come by for a month too (`includeInactive`, as UserMultiSelect
 * has it). Without it the server answers with the active alone, and the troll
 * who has stayed away could not be put on a list at all.
 *
 * What these catch:
 *  - `exclude` read with the case of the caller (the server's logins are not
 *    case-sensitive), or not read at all;
 *  - `includeInactive` not sent as the filter "All", or the filter sent when
 *    nobody asked, which would widen every other name field of the site;
 *  - the open list's Escape let through to what the field stands in;
 *  - an Escape with nothing open swallowed, so the window it stands in could
 *    not be closed from the field;
 *  - the cross of a chosen name against its last letter where the field is
 *    only as wide as what it holds.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import { readFileSync } from "fs";
import { resolve } from "path";
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

/** Types into the field and lets the lookup answer. */
async function typeAndWait(
  wrapper: ReturnType<typeof mountField>["wrapper"],
  text: string,
) {
  const input = wrapper.get("input");
  await input.trigger("focus");
  await input.setValue(text);
  await vi.advanceTimersByTimeAsync(200);
  await flushPromises();
}

const offered = (wrapper: ReturnType<typeof mountField>["wrapper"]) =>
  wrapper.findAll(".autocomplete-item .user-username").map((n) => n.text());

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

  it("не предлагает имена из exclude, в каком бы регистре они ни были", async () => {
    const { wrapper } = mountField(["мордред", "МОРДАУНТ"]);

    await typeAndWait(wrapper, "Мор");

    expect(offered(wrapper)).toEqual(["Моргана"]);
  });

  it("без exclude предлагает все, что нашлось", async () => {
    const { wrapper } = mountField();

    await typeAndWait(wrapper, "Мор");

    expect(offered(wrapper)).toEqual(["Мордред", "Моргана", "Мордаунт"]);
  });

  it("оставляет себе Escape, которым закрывает открытый список", async () => {
    const { wrapper, reached } = mountField();
    await typeAndWait(wrapper, "Мор");
    expect(offered(wrapper)).toHaveLength(3);

    await wrapper.get("input").trigger("keydown", { key: "Escape" });

    expect(wrapper.find(".autocomplete-dropdown").exists()).toBe(false);
    expect(reached).toEqual([]);
  });

  it("пропускает Escape дальше, когда закрывать нечего", async () => {
    const { wrapper, reached } = mountField();

    await wrapper.get("input").trigger("keydown", { key: "Escape" });

    expect(reached).toEqual(["Escape"]);
  });

  it("без includeInactive ищет как прежде, среди активных", async () => {
    const { wrapper } = mountField();

    await typeAndWait(wrapper, "Мор");

    expect(searchUsers).toHaveBeenCalledWith("Мор", 12);
  });

  it("с includeInactive ищет среди всех, давно не заходивших тоже", async () => {
    const { wrapper } = mountField(undefined, false, true);

    await typeAndWait(wrapper, "Мор");

    expect(searchUsers).toHaveBeenCalledWith("Мор", 12, "All");
  });

  it("в окне берет высоту полей окна", () => {
    const { wrapper } = mountField(undefined, true);

    expect(wrapper.get(".user-autocomplete").classes()).toContain(
      "user-autocomplete--dialog",
    );
  });

  // A chosen name in a field only as wide as what it holds (the blacklists,
  // BlacklistAddRow) had its cross against the last letter: the space before
  // the cross was its margin-left auto alone, which comes to nothing there.
  // jsdom lays nothing out, so the rule is read where it is written; the
  // distance itself is measured in e2e/tests/personal/blacklist-look.spec.ts.
  it("ставит крестик в шаге от выбранного имени", () => {
    const style = readFileSync(
      resolve(__dirname, "UserAutocomplete.vue"),
      "utf8",
    ).split("<style")[1];
    const chosen = /\n\.selected-user\n((?:[ \t]+.*\n|\n)*)/.exec(style)?.[1];

    expect(chosen).toMatch(/^ {2}gap: \$small$/m);
    expect(chosen).toMatch(/\.user-avatar\n\s+margin-right: 0/);
  });
});
