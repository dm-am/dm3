/**
 * @vitest-environment jsdom
 */

// The roster read carries no attributes, and a card mounted on it still draws; the roster
// entry below stops compiling the day either field of the detail read stops being optional.
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { SvgIcon } from "@/shared/ui/Icon";
import CharacterCard from "./CharacterCard.vue";
import type { Character } from "../model/characterTypes";

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

  /**
   * The header opens the card on a click and can never open it on a key: it
   * holds a link to the player and the consumer's controls slot, and a
   * role="button" around those hides both from assistive technology. So the
   * chevron is the control, and it has to be a real one — a button carries
   * Enter and Space itself, and its state has to be readable without seeing
   * the card.
   */
  it("gives the disclosure a control the keyboard can reach", async () => {
    const wrapper = mountCard(fromSheet);
    const toggle = wrapper.get("button.expand-toggle");

    expect(toggle.attributes("aria-expanded")).toBe("false");
    expect(toggle.attributes("aria-label")).toBe("Показать полностью");
    expect(toggle.attributes("aria-controls")).toBeTruthy();

    await toggle.trigger("click");

    expect(toggle.attributes("aria-expanded")).toBe("true");
    expect(wrapper.get(".character-details").text()).toContain("Варвар");
    expect(toggle.attributes("aria-controls")).toBe(
      wrapper.get(".character-details").attributes("id"),
    );
  });

  /**
   * The chevron sits inside the header, and the header toggles on click: a
   * click that reaches both handlers opens the card and closes it again in one
   * press. Nothing on screen would show which of the two ran.
   */
  it("does not let the chevron's click reach the header behind it", async () => {
    const wrapper = mountCard(fromSheet);

    await wrapper.get("button.expand-toggle").trigger("click");

    expect(wrapper.find(".character-details").exists()).toBe(true);
  });
});
