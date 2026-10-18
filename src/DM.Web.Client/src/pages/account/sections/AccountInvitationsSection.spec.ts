/**
 * @vitest-environment jsdom
 */

/**
 * The invitations block of the account page.
 *
 * Two clients typed the same endpoint, and this section held the wrong one: the
 * server answers entityId / entityType / entityTitle and the type promised
 * gameId / gameTitle, so every row drew an anchor with no text in it — zero
 * width, unclickable — pointing at /games/undefined. Even filled in, the path
 * was wrong: /games/:id is not a route (the game lives at /game/:id), so it
 * fell through to the catch-all and answered "Страница не найдена".
 *
 * The endpoint returns blog invitations in the same list, which is why the row
 * asks the entity type where to go.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "@/entities/user";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import AccountInvitationsSection from "./AccountInvitationsSection.vue";

/** The section's stylesheet, compiled as the browser gets it, its own @use included. */
const SECTION = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "AccountInvitationsSection.vue",
);
const css = () => compileSass(sassBlocks(SECTION), SECTION);

const invitation = (over: Record<string, unknown> = {}) => ({
  id: "token-1",
  entityId: "11111111-1111-1111-1111-111111111111",
  entityType: "game",
  entityTitle: "Хроники Амбера",
  inviterUsername: "SolohinLex",
  type: "player",
  createdUtc: "2026-08-01T10:00:00Z",
  ...over,
});

async function render(resources: unknown[]) {
  setActivePinia(createPinia());
  vi.spyOn(personalApi, "getMyInvitations").mockResolvedValue({
    data: { resources },
    error: null,
  } as never);

  const wrapper = mount(AccountInvitationsSection, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return wrapper;
}

/** The row's own link to what the viewer was invited to. */
const targetLink = (wrapper: ReturnType<typeof mount>) =>
  wrapper
    .findAllComponents(RouterLinkStub)
    .find((link) => link.classes().includes("invitation-target-link"))!;

/** Answer the accept endpoint the way it answers on success: 204, no body. */
const acceptSucceeds = () =>
  vi
    .spyOn(personalApi, "acceptInvitation")
    .mockResolvedValue({ data: null, error: null } as never);

/** Everything the site is currently saying in a floating message. */
const messages = () => useToast().toasts.value.map((t) => t.message);

describe("AccountInvitationsSection", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Error toasts never auto-dismiss and deduplicate by text.
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("names the game and links to where the game lives", async () => {
    const wrapper = await render([invitation()]);

    const link = targetLink(wrapper);
    expect(link.text()).toBe("Хроники Амбера");
    expect(link.props("to")).toEqual({
      name: "game",
      params: { id: "11111111-1111-1111-1111-111111111111" },
    });
  });

  it("sends a blog invitation to the blog", async () => {
    const wrapper = await render([
      invitation({
        entityType: "blog",
        entityTitle: "Записки",
        type: "reader",
      }),
    ]);

    const link = targetLink(wrapper);
    expect(link.text()).toBe("Записки");
    expect(link.props("to")).toEqual({
      name: "blog",
      params: { id: "11111111-1111-1111-1111-111111111111" },
    });
  });

  it("never writes a path by hand", async () => {
    const wrapper = await render([invitation()]);

    for (const link of wrapper.findAllComponents(RouterLinkStub)) {
      expect(typeof link.props("to")).not.toBe("string");
    }
  });

  /**
   * Accepting used to drop the row and raise "Приглашение принято" over it.
   * Dropping the row showed the half that undoes itself and hid the half that
   * lasts: the server grants a role in the game or the blog and answers 204, so
   * the only place that role was ever named was a message that fades in three
   * seconds. The row stays instead, and says it.
   */
  it("keeps the row and names the role received", async () => {
    const wrapper = await render([invitation()]);
    acceptSucceeds();

    await wrapper.find(".action-btn--accept").trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").text()).toBe(
      "Вы приняты как игрок",
    );
    // The way into the game stays in the row that says the viewer is in it.
    expect(targetLink(wrapper).props("to")).toEqual({
      name: "game",
      params: { id: "11111111-1111-1111-1111-111111111111" },
    });
    // Nothing left to accept or reject in a row that has been accepted.
    expect(wrapper.find(".invitation-actions").exists()).toBe(false);
    // The screen says it, so nothing floats over the screen to say it again.
    expect(messages()).toEqual([]);
  });

  it("names the role a blog invitation gives", async () => {
    const wrapper = await render([
      invitation({
        entityType: "blog",
        entityTitle: "Записки",
        type: "reader",
      }),
    ]);
    acceptSucceeds();

    await wrapper.find(".action-btn--accept").trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").text()).toBe(
      "Вы приняты как читатель",
    );
  });

  it("says nothing was received when the server refuses, under the row", async () => {
    const wrapper = await render([invitation()]);
    // 410: the invitation expired or was already dealt with elsewhere.
    vi.spyOn(personalApi, "acceptInvitation").mockResolvedValue({
      data: null,
      error: { type: "about:blank", title: "", status: 410, traceId: "t" },
    } as never);

    await wrapper.find(".action-btn--accept").trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").exists()).toBe(false);
    // The offer is still on the table, and the refusal is said out loud.
    expect(wrapper.find(".action-btn--accept").exists()).toBe(true);
    // Under the row it was pressed in; no plate in the corner.
    expect(wrapper.get(".invitation-item + .action-result").text()).toBe(
      "Не удалось принять приглашение",
    );
    expect(messages()).toEqual([]);
  });

  it("drops the row on rejection, where there is nothing to stay for", async () => {
    const wrapper = await render([invitation()]);
    vi.spyOn(personalApi, "rejectInvitation").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    await wrapper.find(".action-btn--reject").trigger("click");
    await flushPromises();

    expect(wrapper.findAll(".invitation-item")).toHaveLength(0);
    expect(messages()).toEqual([]);
  });
});

/**
 * Who invited, to what, and when: a caption line of the site. It used to be a
 * row that wrapped, and in the middle column of 1024 it went down a word a
 * line. As a caption line it wraps between its parts only, and nothing of it
 * is put away or cut (owner, 12 October 2026: never shorten on a phone): not
 * the date, not the role, not the inviter's name, nor the title above it.
 */
describe("the line under an invitation", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("is a caption line: from whom, the role, the date", async () => {
    const wrapper = await render([invitation()]);

    const line = wrapper.get(".invitation-details");
    expect(line.classes()).toContain("caption-line");
    expect(line.text()).toBe("От: SolohinLex | Игрок | 01.08.2026");
    expect(line.findAll(".caption-line__part")).toHaveLength(3);
  });

  it("cuts neither the name nor the title", async () => {
    const wrapper = await render([invitation()]);

    const name = wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "SolohinLex")!;
    expect(name.attributes()).not.toHaveProperty("data-fit-shrink");
    const rules = styleRules(css()).filter(({ declarations }) =>
      Object.entries(declarations).some(
        ([property, value]) =>
          property === "text-overflow" ||
          (property === "white-space" && value === "nowrap"),
      ),
    );
    expect(rules.map(({ selector }) => selector)).not.toContain(
      ".invitation-target-link",
    );
  });

  // The title is an item of the flex header, and such an item is never
  // narrower than its longest word: break-word does not lower that, so a title
  // of one word wider than the card went past its edge (measured on the stand
  // at 360, 390 and 1366). With min-width 0 the base rule breaks the word.
  it("lets a title of one long word break inside the card", () => {
    const title = styleRules(css()).find(
      ({ selector }) => selector === ".invitation-target-link",
    );
    expect(title?.declarations["min-width"]).toBe("0");
  });

  // A phone shows the row as the full version does: the words on the left,
  // "Принять" and "Отклонить" beside them on the right. The list used to put
  // the buttons under the words where it was narrower than 30em.
  it("has no form of its own for a narrow list", () => {
    const compiled = css();
    expect(compiled).not.toContain("@container");
    expect(compiled).not.toContain("@media");
    const row = styleRules(compiled).find(
      ({ selector }) => selector === ".invitation-item",
    );
    expect(row?.declarations["justify-content"]).toBe("space-between");
    expect(row?.declarations["flex-direction"]).toBeUndefined();
  });
});
