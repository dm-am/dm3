/**
 * @vitest-environment jsdom
 */

/**
 * The first read of a section of the account: while it is out the section
 * stands in the shape of what it will show, with no word; what is not there is
 * said only once the server answered; a read that failed is the shared block
 * with "Повторить", which reads again with no reload of the page.
 */
import { afterEach, describe, expect, it, vi, type MockInstance } from "vitest";
import type { Component } from "vue";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, blacklistApi, personalApi } from "@/entities/user";
import AccountBlacklistSection from "./AccountBlacklistSection.vue";
import AccountInvitationsSection from "./AccountInvitationsSection.vue";
import AccountNotificationsSection from "./AccountNotificationsSection.vue";
import AccountSecurityHistorySection from "./AccountSecurityHistorySection.vue";
import AccountSessionsSection from "./AccountSessionsSection.vue";

enableAutoUnmount(afterEach);
afterEach(() => vi.restoreAllMocks());

const FAILED = {
  data: null,
  error: { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
};

interface Section {
  name: string;
  component: Component;
  /** The first read of the section, spied on before it mounts. */
  read: () => MockInstance;
  /** What the server answers when it holds nothing. */
  nothing: unknown;
  /** The words of the section once the read failed. */
  failure: string;
  /** What the section says once it is read and holds nothing, if it says anything. */
  empty?: string;
  /** Reads of the section that are not the subject here. */
  quiet?: () => void;
}

const SECTIONS: Section[] = [
  {
    name: "Сессии",
    component: AccountSessionsSection,
    read: () => vi.spyOn(accountApi, "getSessions"),
    nothing: { data: { resources: [] }, error: null },
    failure: "Не удалось загрузить сессии",
    empty: "Нет активных сессий",
  },
  {
    name: "Журнал безопасности",
    component: AccountSecurityHistorySection,
    read: () => vi.spyOn(accountApi, "getSecurityHistory"),
    nothing: { data: { resources: [] }, error: null },
    failure: "Не удалось загрузить журнал безопасности",
    empty: "Нет событий безопасности",
  },
  {
    name: "Приглашения",
    component: AccountInvitationsSection,
    read: () => vi.spyOn(personalApi, "getMyInvitations"),
    nothing: { data: { resources: [] }, error: null },
    failure: "Не удалось загрузить приглашения",
  },
  {
    name: "Уведомления",
    component: AccountNotificationsSection,
    read: () => vi.spyOn(accountApi, "getNotificationPreferences"),
    nothing: { data: { telegram: undefined, discord: undefined }, error: null },
    failure: "Не удалось загрузить настройки уведомлений",
    empty: "Не подключен",
  },
  {
    name: "Черный список",
    component: AccountBlacklistSection,
    read: () => vi.spyOn(blacklistApi, "getSettings"),
    nothing: {
      data: {
        hideComments: false,
        hideMessages: false,
        hideGames: false,
        hideBlogs: false,
        blockDirectMessages: false,
        autoPopulateContentBlacklist: false,
      },
      error: null,
    },
    failure: "Не удалось загрузить настройки черного списка",
    quiet: () =>
      vi.spyOn(blacklistApi, "getBlacklist").mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      } as never),
  },
];

function open(section: Section): VueWrapper {
  setActivePinia(createPinia());
  section.quiet?.();
  return mount(section.component, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
}

const cases = SECTIONS.map((section) => [section.name, section] as const);

describe("the first read of a section of the account", () => {
  it.each(cases)(
    "%s: stands in its shape with no word while the read is out",
    async (_name, section) => {
      section.read().mockReturnValue(new Promise(() => {}) as never);

      const wrapper = open(section);
      await flushPromises();

      expect(wrapper.find(".account-skeleton").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("Загрузка");
      if (section.empty) expect(wrapper.text()).not.toContain(section.empty);
      expect(wrapper.find(".error-state").exists()).toBe(false);
    },
  );

  it.each(cases)(
    "%s: says a failed read in the shared block, not as nothing held",
    async (_name, section) => {
      section.read().mockResolvedValue(FAILED as never);

      const wrapper = open(section);
      await flushPromises();

      expect(wrapper.get(".error-state").text()).toContain(section.failure);
      expect(wrapper.find(".account-skeleton").exists()).toBe(false);
      if (section.empty) expect(wrapper.text()).not.toContain(section.empty);
    },
  );

  it.each(cases)(
    "%s: reads again on Повторить and then says what it holds",
    async (_name, section) => {
      const read = section
        .read()
        .mockResolvedValueOnce(FAILED as never)
        .mockResolvedValue(section.nothing as never);

      const wrapper = open(section);
      await flushPromises();
      await wrapper.get(".error-retry").trigger("click");
      await flushPromises();

      expect(read).toHaveBeenCalledTimes(2);
      expect(wrapper.find(".error-state").exists()).toBe(false);
      if (section.empty) expect(wrapper.text()).toContain(section.empty);
    },
  );
});
