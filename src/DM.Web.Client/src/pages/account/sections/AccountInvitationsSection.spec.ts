/**
 * @vitest-environment jsdom
 */

/**
 * The invitations block of the account page: each row links to its game or its
 * blog by route name, an accepted row stays and names the role, a refusal
 * stands under its row, a rejected row leaves.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
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
    .find((link) => link.classes().includes("invitation-target-link"))!;

/** Answer the accept endpoint the way it answers on success: 204, no body. */
const acceptSucceeds = () =>
  vi
    .spyOn(personalApi, "acceptInvitation")
    .mockResolvedValue({ data: null, error: null } as never);

/** The button of the row that reads `label`. */
const button = (wrapper: ReturnType<typeof mount>, label: string) =>
  wrapper.findAll("button").find((b) => b.text() === label);

/** Everything the site is currently saying in a floating message. */
const messages = () => useToast().toasts.value.map((t) => t.message);

describe("AccountInvitationsSection", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Error toasts never dismiss themselves and deduplicate by text.
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

  /** Accepting keeps the row and names the role the server granted. */
  it("keeps the row and names the role received", async () => {
    const wrapper = await render([invitation()]);
    acceptSucceeds();

    await button(wrapper, "Принять")!.trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").text()).toBe(
      "Вы приняты как игрок",
    );
    // The way into the game stays in the row.
    expect(targetLink(wrapper).props("to")).toEqual({
      name: "game",
      params: { id: "11111111-1111-1111-1111-111111111111" },
    });
    // Nothing left to accept or reject.
    expect(wrapper.find(".invitation-actions").exists()).toBe(false);
    // The screen says it, so nothing floats over it.
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

    await button(wrapper, "Принять")!.trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").text()).toBe(
      "Вы приняты как читатель",
    );
  });

  it("says nothing was received when the server refuses, under the row", async () => {
    const wrapper = await render([invitation()]);
    // 410: the invitation expired or was dealt with elsewhere.
    vi.spyOn(personalApi, "acceptInvitation").mockResolvedValue({
      data: null,
      error: { type: "about:blank", title: "", status: 410, traceId: "t" },
    } as never);

    await button(wrapper, "Принять")!.trigger("click");
    await flushPromises();

    expect(wrapper.find(".invitation-accepted").exists()).toBe(false);
    // The offer is still there.
    expect(button(wrapper, "Принять")).toBeDefined();
    // Under the row it was pressed in, no plate in the corner.
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

    await button(wrapper, "Отклонить")!.trigger("click");
    await flushPromises();

    expect(wrapper.findAll(".invitation-item")).toHaveLength(0);
    expect(messages()).toEqual([]);
  });
});

/** Who invited, to what and when, in a caption line of the site that cuts nothing. */
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
});
