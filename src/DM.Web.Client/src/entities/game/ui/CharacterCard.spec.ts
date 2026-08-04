/**
 * @vitest-environment jsdom
 */

/**
 * The roster and the sheet are two different reads, and the card has to
 * survive the lighter one. The list endpoint answers names, portraits,
 * statuses and authors; the filled-in attributes come from the
 * single-character read. That split is deliberate and load-bearing: the game
 * shell fetches the roster on every page of the game zone, to find the NPCs
 * for one menu item, and putting the sheet on that response would ship every
 * character's server-rendered BbCode along with it.
 *
 * The card used to read the attributes unconditionally and threw inside its
 * render, and Vue drops the whole subtree on a render error — the characters
 * page drew the game title and nothing else, no cards and no message.
 *
 * The guard has two halves. At runtime a card mounted on a roster entry still
 * draws the name and offers no chevron to open. At compile time the roster
 * entry is declared without the two fields only the detail read carries and
 * assigned to Character: the day either of them stops being optional, this
 * file stops compiling.
 */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { SvgIcon } from "@/shared/ui/Icon";
import CharacterCard from "./CharacterCard.vue";
import type { Character } from "../model/types";

/**
 * A roster entry, field for field as the list endpoint sends it. Ids and most
 * scalars are branded, so the literal is asserted rather than annotated; the
 * assignment below is what the compiler actually checks.
 */
const rosterEntry = {
  id: "char-1",
  name: "Ронин",
  status: "Active",
  isDead: false,
  isPlayerLeft: false,
  isPlayerExiled: false,
  isNpc: false,
  author: {
    id: "user-1",
    username: "gamer",
    lastActivityUtc: null,
    role: "Player",
    isNewbie: false,
  },
  picture: {},
  totalPostsCount: 3,
  lastPostUtc: "2026-08-02T14:54:58",
  descriptor: "Варвар",
} as unknown as Omit<Character, "attributes" | "privacy">;

/** The compile-time half of the guard. */
const fromRoster: Character = rosterEntry;

/** The same character as the single-character read answers it. */
const fromSheet = {
  ...rosterEntry,
  attributes: [{ id: "spec-1", title: "Класс", value: "Варвар" }],
} as unknown as Character;

const mountCard = (character: Character) =>
  mount(CharacterCard, {
    props: { character },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });

describe("CharacterCard", () => {
  it("draws a roster entry that carries no sheet", () => {
    const wrapper = mountCard(fromRoster);

    expect(wrapper.get(".character-name").text()).toBe("Ронин");
    expect(wrapper.findComponent(SvgIcon).exists()).toBe(false);
  });

  it("opens the sheet the single-character read supplies", async () => {
    const wrapper = mountCard(fromSheet);

    expect(wrapper.findComponent(SvgIcon).exists()).toBe(true);
    await wrapper.get(".character-header").trigger("click");

    expect(wrapper.get(".character-details").text()).toContain("Класс");
    expect(wrapper.get(".character-details").text()).toContain("Варвар");
  });
});
