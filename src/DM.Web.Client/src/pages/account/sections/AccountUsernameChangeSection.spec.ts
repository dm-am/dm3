/**
 * @vitest-environment jsdom
 */

/**
 * The account section that asks for a name change and reports on the asking.
 *
 * The flow reaches five states and the section knew three. `GET
 * /account/username-change` returns the latest request whatever state it is in,
 * so an expired or completed one arrives here too, and both used to render
 * nothing at all: no card said what happened, and the form stayed hidden because
 * only a rejection reopened it. A request that ran out of moderation time was
 * therefore a dead end with no explanation and no way to ask again - while the
 * server, which is the only side that decides anything, was already accepting a
 * new request.
 *
 * Asserted per status because the two sides of the section answer different
 * questions: which card is shown is what the reader is told, and whether the
 * form is there is what they can do about it.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountUsernameChangeSection from "./AccountUsernameChangeSection.vue";
import type { UsernameChangeRequest } from "@/shared/api/models/account";

const user = { id: "u-1", username: "reader" };

const options = () => ({
  props: { user } as never,
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
});

/** Mounts the section over one stored request, or over none at all. */
const withRequest = async (request: Partial<UsernameChangeRequest> | null) => {
  vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
    data: request
      ? ({
          id: "r-1",
          currentUsername: "reader",
          reason: "Хочу другое имя",
          createdUtc: "2026-05-01T10:00:00Z",
          ...request,
        } as UsernameChangeRequest)
      : null,
    error: null,
  } as never);

  const wrapper = mount(AccountUsernameChangeSection, options());
  await flushPromises();
  return wrapper;
};

describe("username change section", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("offers the form when nothing was ever asked", async () => {
    const wrapper = await withRequest(null);

    expect(wrapper.find(".request-form").exists()).toBe(true);
    expect(wrapper.find(".status-card").exists()).toBe(false);
  });

  it.each([
    ["Pending", "Заявка на рассмотрении"],
    ["Approved", "Заявка одобрена"],
    ["Rejected", "Заявка отклонена"],
    ["Expired", "Заявка истекла"],
    ["Completed", "Имя изменено"],
  ])("names the state of a %s request", async (status, title) => {
    const wrapper = await withRequest({
      status: status as UsernameChangeRequest["status"],
    });

    expect(wrapper.find(".status-card").text()).toContain(title);
  });

  it.each([
    // In flight: the change was already asked for, and the server refuses a second.
    ["Pending", false],
    ["Approved", false],
    // Holding nothing: asking again is the only way forward.
    ["Rejected", true],
    ["Expired", true],
    ["Completed", true],
  ])("decides on the form after a %s request", async (status, offered) => {
    const wrapper = await withRequest({
      status: status as UsernameChangeRequest["status"],
    });

    expect(wrapper.find(".request-form").exists()).toBe(offered);
  });

  it("tells an unreviewed expiry from a lapsed approval", async () => {
    const unreviewed = await withRequest({
      status: "Expired",
      expiryReason: "Unreviewed",
    });
    expect(unreviewed.find(".status-note").text()).toContain(
      "не рассмотрели заявку",
    );

    const lapsed = await withRequest({
      status: "Expired",
      expiryReason: "ApprovalLapsed",
    });
    expect(lapsed.find(".status-note").text()).toContain("была одобрена");
  });

  it("reads an expiry with no reason as unreviewed", async () => {
    // The field is the server's to set. An older payload without it must not
    // claim an approval that may never have happened.
    const wrapper = await withRequest({ status: "Expired" });

    expect(wrapper.find(".status-note").text()).toContain(
      "не рассмотрели заявку",
    );
  });

  it("does not offer the form when the state could not be read", async () => {
    // Offering it here promises something the server may refuse: the request may
    // well be in flight, and the reply that says so would land as a note under
    // the reason field, where there is nothing to correct.
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: { status: 500, message: "Ошибка сервера" },
    } as never);

    const wrapper = mount(AccountUsernameChangeSection, options());
    await flushPromises();

    expect(wrapper.find(".request-form").exists()).toBe(false);
    expect(wrapper.find(".status-card--failed").exists()).toBe(true);
  });

  it("re-reads the request when the server answers a conflict", async () => {
    const wrapper = await withRequest(null);
    expect(wrapper.find(".request-form").exists()).toBe(true);

    // Filed from somewhere else in the meantime: the section is stale, and the
    // card is where the reader learns that, not the field they typed into.
    vi.spyOn(accountApi, "createUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: { status: 409, message: "Заявка на смену имени уже отправлена" },
    } as never);
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
      data: {
        id: "r-2",
        currentUsername: "reader",
        reason: "Хочу другое имя",
        createdUtc: "2026-05-01T10:00:00Z",
        status: "Pending",
      },
      error: null,
    } as never);

    await wrapper.find("textarea").setValue("Причина достаточной длины");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find(".status-card").text()).toContain(
      "Заявка на рассмотрении",
    );
    expect(wrapper.find(".request-form").exists()).toBe(false);
  });

  it("shows the resolution comment on an expired request", async () => {
    const wrapper = await withRequest({
      status: "Expired",
      resolverComment: "Токен истек: пользователь не выбрал новое имя",
    });

    expect(wrapper.find(".status-card").text()).toContain("Токен истек");
  });
});
