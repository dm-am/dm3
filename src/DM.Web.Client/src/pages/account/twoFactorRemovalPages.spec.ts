/**
 * @vitest-environment jsdom
 */

/**
 * The two pages the letters about taking a second factor off point at.
 *
 * They differ on purpose, and the difference is the thing worth a gate. The
 * page that SCHEDULES a removal waits for a press: following it ends every
 * session of the account and starts a seven-day countdown, and links in letters
 * are opened by scanners and previews as well as by people. The page that CALLS
 * a scheduled removal OFF fires on open, because it undoes rather than starts.
 *
 * Neither has a "check this token" call to lean on - the server has none - so a
 * dead link is learnt from the answer to the action, and the sentence the
 * server sends is the sentence shown.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: {
      RouterLink: { template: "<a><slot /></a>" },
      PageTitle: { template: "<div><slot /></div>" },
    },
  },
});

beforeEach(() => {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
  // The value arrives in the fragment: never sent to a server, so it is in no
  // access log and in no Referer.
  window.location.hash = "#token=a-token";
});

describe("scheduling a removal from the letter", () => {
  it("waits for a press instead of acting on open", async () => {
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Назначить снятие");
    expect(wrapper.text()).toContain("через семь дней");
  });

  it("says the link is dead without pretending anything was scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: {
        status: 403,
        title:
          "Снять второй фактор по почте нельзя: обратитесь ко второму администратору",
      },
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    await wrapper.find(".status-actions button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("обратитесь ко второму администратору");
    expect(wrapper.text()).not.toContain("Снятие назначено");
  });

  it("names the consequences once the removal is scheduled", async () => {
    vi.spyOn(accountApi, "scheduleTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();
    await wrapper.find(".status-actions button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Снятие назначено");
    expect(wrapper.text()).toContain("Все сессии аккаунта завершены");
    expect(wrapper.text()).toContain("войдите со вторым фактором");
  });

  it("does not offer the press when the link carries no value", async () => {
    window.location.hash = "";
    const schedule = vi.spyOn(accountApi, "scheduleTwoFactorRemoval");

    const wrapper = mount(TwoFactorRemovalPage, options());
    await flushPromises();

    expect(schedule).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Ссылка недействительна");
  });
});

describe("calling a scheduled removal off", () => {
  it("acts on open", async () => {
    const cancel = vi
      .spyOn(accountApi, "cancelTwoFactorRemoval")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();

    expect(cancel).toHaveBeenCalledWith("a-token");
    expect(wrapper.text()).toContain("Снятие отменено");
  });

  it("shows the refusal instead of claiming the removal is off", async () => {
    vi.spyOn(accountApi, "cancelTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 410, title: "Ссылка недействительна или использована" },
    } as never);

    const wrapper = mount(TwoFactorRemovalCancelPage, options());
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка недействительна или использована");
    expect(wrapper.text()).not.toContain("Снятие отменено");
  });
});
