/**
 * @vitest-environment jsdom
 */

/** The pages a letter opens: one card for the family, and a line while the link is checked. */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { Component } from "vue";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";
import { accountApi } from "@/entities/user";
import AccountActivationPage from "./AccountActivationPage.vue";
import PasswordResetPage from "./PasswordResetPage.vue";
import EmailChangePage from "./EmailChangePage.vue";
import UsernameChangePage from "./UsernameChangePage.vue";
import TwoFactorRemovalPage from "./TwoFactorRemovalPage.vue";
import TwoFactorRemovalCancelPage from "./TwoFactorRemovalCancelPage.vue";

// The value arrives in the fragment.
beforeEach(() => {
  window.location.hash = "#token=a-token";
});

/** A request that never settles: the page stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

/** The pages by the addresses their letters carry, and where their cards lead. */
const routes: Address[] = [
  ["activation", "/activate"],
  ["reset-password", "/reset-password"],
  ["confirm-email", "/confirm-email"],
  ["change-username", "/change-username"],
  ["two-factor-removal", "/two-factor/removal"],
  ["two-factor-removal-cancel", "/two-factor/removal/cancel"],
  ["home", "/"],
  ["support", "/support"],
];

const ownAddress = new Map<Component, string>([
  [AccountActivationPage, "/activate"],
  [PasswordResetPage, "/reset-password"],
  [EmailChangePage, "/confirm-email"],
  [UsernameChangePage, "/change-username"],
  [TwoFactorRemovalPage, "/two-factor/removal"],
  [TwoFactorRemovalCancelPage, "/two-factor/removal/cancel"],
]);

/** Open a page at the address of its letter. */
const open = async (page: Component) => {
  const own = ownAddress.get(page);
  if (!own) throw new Error("a page with no letter of its own");
  return (await mountPage(page, own, { routes })).wrapper;
};

describe("pages reached from a letter", () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.restoreAllMocks();
  });

  /** The card says it is checking and nothing about a failure that has not happened. */
  it.each([
    ["the activation card", AccountActivationPage, "getActivationInfo"],
    ["the reset card", PasswordResetPage, "getPasswordResetTokenInfo"],
  ] as const)(
    "says nothing about a failure in %s yet",
    async (_, page, endpoint) => {
      vi.spyOn(accountApi, endpoint).mockImplementation(pending);

      const wrapper = await open(page);
      const card = wrapper.text();

      expect(card).toContain("Проверяем ссылку...");
      for (const failure of ["устарел", "истек", "не найден", "Выберите имя"]) {
        expect(card).not.toContain(failure);
      }

      // No help while nothing has gone wrong.
      expect(wrapper.find(".auth-footnote").exists()).toBe(false);
      expect(card.trim()).toBe("Проверяем ссылку...");
    },
  );

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

      const wrapper = await open(page);
      await flushPromises();
      const card = wrapper.get(".auth-surface > .auth-card").classes();

      expect(card).toContain("auth-card--framed");
      expect(card.includes("auth-card--center")).toBe(narrow);
      expect(card.includes("auth-card--compact")).toBe(narrow);
      expect(wrapper.get("h1").classes()).toContain("auth-title");
    },
  );
});
