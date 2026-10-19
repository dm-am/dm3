/**
 * @vitest-environment jsdom
 */

/**
 * The account section that asks for a name change and reports on the asking,
 * per status: which card is shown, and whether the form is there to ask again.
 */
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
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
    // In flight: the change is asked for, and the server refuses a second.
    ["Pending", false],
    ["Approved", false],
    // Holding nothing: asking again is the way forward.
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
    // An older payload without the field must not claim an approval.
    const wrapper = await withRequest({ status: "Expired" });

    expect(wrapper.find(".status-note").text()).toContain(
      "не рассмотрели заявку",
    );
  });

  it("does not offer the form when the state could not be read", async () => {
    // The request may be in flight, and the refusal would land under the reason field.
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: { status: 500, message: "Ошибка сервера" },
    } as never);

    const wrapper = mount(AccountUsernameChangeSection, options());
    await flushPromises();

    expect(wrapper.find(".request-form").exists()).toBe(false);
    expect(wrapper.find(".error-state").exists()).toBe(true);
  });

  it("reads the state again on Повторить, with no reload of the page", async () => {
    const load = vi
      .spyOn(accountApi, "getUsernameChangeRequest")
      .mockResolvedValueOnce({
        data: null,
        error: { status: 500, title: "Ошибка сервера" },
      } as never)
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mount(AccountUsernameChangeSection, options());
    await flushPromises();
    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(load).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".request-form").exists()).toBe(true);
  });

  it("stands a placeholder and no word while the state is read", async () => {
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockReturnValue(
      new Promise(() => {}) as never,
    );

    const wrapper = mount(AccountUsernameChangeSection, options());
    await flushPromises();

    expect(wrapper.find(".account-skeleton").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Загрузка");
    expect(wrapper.find(".request-form").exists()).toBe(false);
  });

  it("re-reads the request when the server answers a conflict", async () => {
    const wrapper = await withRequest(null);
    expect(wrapper.find(".request-form").exists()).toBe(true);

    // Filed elsewhere in the meantime: the card says so, not the field.
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

    await wrapper.find("textarea").setValue("Хочу другое имя");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find(".status-card").text()).toContain(
      "Заявка на рассмотрении",
    );
    expect(wrapper.find(".request-form").exists()).toBe(false);
  });

  it("sends a reason of one character", async () => {
    const wrapper = await withRequest(null);
    const create = vi
      .spyOn(accountApi, "createUsernameChangeRequest")
      .mockResolvedValue({ data: null, error: null } as never);

    await wrapper.find("textarea").setValue("Я");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(create).toHaveBeenCalledWith({ reason: "Я" });
  });

  it("sends nothing while the reason is only spaces", async () => {
    const wrapper = await withRequest(null);
    const create = vi.spyOn(accountApi, "createUsernameChangeRequest");

    await wrapper.find("textarea").setValue("   ");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".request-submit").attributes("disabled")).toBeDefined();
    expect(create).not.toHaveBeenCalled();
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

  /** The read takes the refusal and the limit over, checked at the call. */
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

  /** An ended session and a request that never left keep the section's own line. */
  it.each([0, 401])("keeps its own line for a %i", async (status) => {
    const wrapper = await failedRead({ status, title: "Unauthorized" });

    const card = wrapper.get(".error-state");
    expect(card.text()).toContain("Не удалось узнать состояние заявки");
    expect(card.text()).not.toContain("Unauthorized");
    expect(wrapper.find(".request-form").exists()).toBe(false);
  });

  /** A refusal of the press is said under the reason field, the form still there. */
  it("says a 500 on the press under the reason field", async () => {
    const wrapper = await withRequest(null);
    vi.spyOn(accountApi, "createUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);

    await wrapper.find("textarea").setValue("Хочу другое имя");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".form-field-error").text()).toBe("Ошибка сервера");
    // Nothing was filed, and the form is still there.
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

// The profile's "Сменить имя пользователя" lands here while the blocks above
// still load: the section is put back at the top on every change of the page's
// height, until the reader takes the scroll.
describe("username change section as the anchor of the address", () => {
  /** The callback of the one observer the section starts, and what it watches. */
  let resized: (() => void) | null;
  let watched: Element | null;
  let scrolled: ReturnType<typeof vi.fn>;
  const jsdomScroll = Element.prototype.scrollIntoView;

  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    resized = null;
    watched = null;
    vi.stubGlobal(
      "ResizeObserver",
      class {
        constructor(callback: () => void) {
          resized = callback;
        }
        observe(target: Element) {
          watched = target;
        }
        disconnect() {
          resized = null;
        }
      },
    );
    scrolled = vi.fn();
    Element.prototype.scrollIntoView = scrolled as never;
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    Element.prototype.scrollIntoView = jsdomScroll;
    window.location.hash = "";
  });

  async function mountInPage() {
    vi.spyOn(accountApi, "getUsernameChangeRequest").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const page = document.createElement("div");
    document.body.appendChild(page);
    const wrapper = mount(AccountUsernameChangeSection, {
      ...options(),
      attachTo: page,
    });
    await flushPromises();
    return wrapper;
  }

  it("brings itself back to the top as the page around it settles", async () => {
    window.location.hash = "#username-change";
    const wrapper = await mountInPage();

    expect(watched).toBe(wrapper.element.parentElement);
    resized?.();
    resized?.();

    expect(scrolled).toHaveBeenCalledTimes(2);
    expect(scrolled.mock.contexts[0]).toBe(wrapper.element);
    wrapper.unmount();
  });

  it("lets the reader take the scroll over", async () => {
    window.location.hash = "#username-change";
    const wrapper = await mountInPage();

    window.dispatchEvent(new Event("wheel"));

    expect(resized).toBeNull();
    wrapper.unmount();
  });

  it("holds nothing when the address names no section", async () => {
    const wrapper = await mountInPage();

    expect(resized).toBeNull();
    wrapper.unmount();
  });
});
