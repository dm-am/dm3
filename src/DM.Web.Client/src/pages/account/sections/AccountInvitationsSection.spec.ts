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
import AccountInvitationsSection from "./AccountInvitationsSection.vue";

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
    .find((link) => link.classes().includes("invitation-game-link"))!;

describe("AccountInvitationsSection", () => {
  beforeEach(() => vi.restoreAllMocks());

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
});
