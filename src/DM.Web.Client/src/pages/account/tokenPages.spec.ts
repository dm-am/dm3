/**
 * @vitest-environment jsdom
 */

/** The pages a letter opens: one card for the family, and a line while the link is checked. */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountActivationPage from "./AccountActivationPage.vue";
import PasswordResetPage from "./PasswordResetPage.vue";
import EmailChangePage from "./EmailChangePage.vue";
import UsernameChangePage from "./UsernameChangePage.vue";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";

vi.mock("vue-router", async () =>
  (await import("./guestPages.doubles")).vueRouter(),
);

// The value arrives in the fragment.
beforeEach(() => {
  window.location.hash = "#token=a-token";
});

/** A request that never settles: the page stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
});

describe("pages reached from a letter", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  /** The card says it is checking and nothing about a failure that has not happened. */
  it.each([
    ["the activation card", AccountActivationPage, "getActivationInfo"],
    ["the reset card", PasswordResetPage, "getPasswordResetTokenInfo"],
  ] as const)("says nothing about a failure in %s yet", (_, page, endpoint) => {
    vi.spyOn(accountApi, endpoint).mockImplementation(pending);

    const wrapper = mount(page, options());
    const card = wrapper.text();

    expect(card).toContain("Проверяем ссылку...");
    for (const failure of ["устарел", "истек", "не найден", "Выберите имя"]) {
      expect(card).not.toContain(failure);
    }

    // No help while nothing has gone wrong.
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
    expect(card.trim()).toBe("Проверяем ссылку...");
  });

  /** The surface draws the card; the wide measure is kept for the one form that takes a password. */
  it.each([
    ["активация, ссылка недействительна", AccountActivationPage, "", true],
    ["сброс пароля, ссылка недействительна", PasswordResetPage, "", true],
    ["сброс пароля, новый пароль", PasswordResetPage, "#token=a-token", false],
    ["смена почты, ссылка недействительна", EmailChangePage, "", true],
    ["смена имени, ссылка недействительна", UsernameChangePage, "", true],
    [
      "отключение защиты, ссылка недействительна",
      TwoFactorRemovalPage,
      "",
      true,
    ],
    [
      "отмена отключения, ссылка недействительна",
      TwoFactorRemovalCancelPage,
      "",
      true,
    ],
  ] as const)(
    "draws %s on the guest surface",
    async (_, page, hash, narrow) => {
      window.location.hash = hash;
      vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue(
        (hash
          ? { data: { status: "ready" }, error: null }
          : { data: null, error: { status: 404 } }) as never,
      );

      const wrapper = mount(page, options());
      await flushPromises();
      const card = wrapper.get(".auth-surface > .auth-card").classes();

      expect(card).toContain("auth-card--framed");
      expect(card.includes("auth-card--center")).toBe(narrow);
      expect(card.includes("auth-card--compact")).toBe(narrow);
      expect(wrapper.get("h1").classes()).toContain("auth-title");
    },
  );
});
