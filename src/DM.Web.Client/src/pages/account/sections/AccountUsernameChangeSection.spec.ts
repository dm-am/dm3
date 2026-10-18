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
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { readFileSync } from "node:fs";

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

  /** Mounts the section over a read the server refused. */
  const failedRead = async (error: Record<string, unknown>) => {
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
      data: null,
      error,
    } as never);

    const wrapper = mount(AccountUsernameChangeSection, options());
    await flushPromises();
    return wrapper;
  };

  /**
   * The card that stands in place of a failed read is the one carrier of that
   * failure: the section shows nothing else, and the form under it is shut. So
   * the read claims the refusal and the limit, and nothing repeats the card's
   * sentence in a plate one line above it.
   *
   * Asserted at the call, because the request is mocked out in every other test
   * of this file and a flag left behind would be invisible in all of them.
   */
  it("takes the refusal and the limit over on the read", async () => {
    const load = vi
      .spyOn(accountApi, "getUsernameChangeRequest")
      .mockResolvedValue({ data: null, error: null } as never);

    mount(AccountUsernameChangeSection, options());
    await flushPromises();

    expect(load).toHaveBeenCalledWith(
      expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
    );
  });

  /**
   * And having taken it over, the card says what the server said. Its own line
   * names only that something is missing; the refusal names why the form under
   * it is shut, and it used to arrive in a plate that fades after eight seconds
   * over a card that stays.
   */
  it.each([403, 500])(
    "prints what the server said about a %i",
    async (status) => {
      const wrapper = await failedRead({
        status,
        title: "Заявки недоступны",
      });

      const card = wrapper.get(".status-card--failed");
      expect(card.text()).toContain("Заявки недоступны");
      expect(card.text()).not.toContain("Не удалось узнать состояние");
      // Whatever the sentence, the gate under it holds.
      expect(wrapper.find(".request-form").exists()).toBe(false);
    },
  );

  /**
   * The wait is built and not relayed: the seconds the server named ride in
   * Retry-After, which the transport copies onto the failure, while the title
   * of a 429 carries no number at all - and the number is the only part of a
   * wait a reader can act on.
   */
  it("names the seconds when the limit refused the read", async () => {
    const wrapper = await failedRead({
      status: 429,
      title: "Слишком много запросов",
      retryAfterSeconds: 30,
    });

    const card = wrapper.get(".status-card--failed");
    expect(card.text()).toContain(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
    expect(card.text()).not.toContain("Слишком много запросов");
  });

  /**
   * The two the interceptor still answers for, and knows more about than this
   * section does: a session that ended is about the session, and a request that
   * never went out has no sentence of its own. The card names what is missing,
   * the plate says why.
   */
  it.each([0, 401])("keeps its own line for a %i", async (status) => {
    const wrapper = await failedRead({ status, title: "Unauthorized" });

    const card = wrapper.get(".status-card--failed");
    expect(card.text()).toContain("Не удалось узнать состояние заявки");
    expect(card.text()).not.toContain("Unauthorized");
    expect(wrapper.find(".request-form").exists()).toBe(false);
  });

  // Nobody else speaks for these, so the card relays what the server named.
  it("relays a refusal the interceptor keeps quiet about", async () => {
    const wrapper = await failedRead({
      status: 400,
      title: "Заявка не может быть прочитана",
    });

    expect(wrapper.get(".status-card--failed").text()).toContain(
      "Заявка не может быть прочитана",
    );
  });

  /**
   * The press answers under the reason field, the refusals the interceptor
   * used to plate included: the form stays on screen whatever happens, and the
   * plate is taken down (refusalHere).
   */
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
  ])("says a %i on the press under the reason field", async (status, line) => {
    const wrapper = await withRequest(null);
    vi.spyOn(accountApi, "createUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: { status, title: "Ошибка сервера" },
    } as never);

    await wrapper.find("textarea").setValue("Причина достаточной длины");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".form-field-error").text()).toBe(line);
    // Refused all the same: nothing was filed, and the form is still there.
    expect(wrapper.find(".request-form").exists()).toBe(true);
    expect(wrapper.find(".status-card").exists()).toBe(false);
  });

  it("shows the resolution comment on an expired request", async () => {
    const wrapper = await withRequest({
      status: "Expired",
      resolverComment: "Токен истек: пользователь не выбрал новое имя",
    });

    expect(wrapper.find(".status-card").text()).toContain("Токен истек");
  });
});

// A state is written in words on this site and not drawn as a panel: the
// pills of the account page went for that reason, and these cards wore the
// same thing at the size of a block, a wash of colour in a frame of it with a
// mark beside the words. What is left of a card is the sentence in the colour
// of what it means.
describe("the state of a username change request", () => {
  const file = join(
    dirname(fileURLToPath(import.meta.url)),
    "AccountUsernameChangeSection.vue",
  );
  const rules = () =>
    styleRules(compileSass(sassBlocks(file), file)).filter(({ selector }) =>
      /status-(card|header|title|icon)/.test(selector),
    );

  it("is not a panel: no fill and no frame", () => {
    const panels = rules().filter(({ declarations }) =>
      Object.keys(declarations).some((name) =>
        /^(background|border)/.test(name),
      ),
    );
    expect(panels).toEqual([]);
  });

  it("has no mark beside its words", () => {
    expect(
      rules().filter(({ selector }) => /status-icon/.test(selector)),
    ).toEqual([]);
    expect(readFileSync(file, "utf8")).not.toContain("status-icon");
  });
});
