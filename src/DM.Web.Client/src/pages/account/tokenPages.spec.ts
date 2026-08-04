/**
 * @vitest-environment jsdom
 */

/**
 * The two pages a person lands on from a letter, while they are checking the
 * link.
 *
 * Both declared a "loading" state and neither had a branch for it: the card
 * rendered empty, with "Нужна помощь? Обратитесь в поддержку" underneath — the
 * signal that something is broken, shown at the moment nothing is. The third
 * page of the family, EmailChangePage, has said "Подтверждаем смену почты..."
 * all along.
 *
 * One gate for the pair: they are the same defect twice.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import AccountActivationPage from "./AccountActivationPage.vue";
import PasswordResetPage from "./PasswordResetPage.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { token: "a-token" }, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

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

  it("tells the reader the activation link is being checked", () => {
    vi.spyOn(accountApi, "getActivationInfo").mockImplementation(pending);

    const wrapper = mount(AccountActivationPage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
  });

  it("tells the reader the password-reset link is being checked", () => {
    vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockImplementation(
      pending,
    );

    const wrapper = mount(PasswordResetPage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
  });

  /**
   * The support line sits outside every branch and is drawn in all of them,
   * loading included — which is fine now that the card says what it is doing,
   * and was the whole card when it did not. What has to hold is that the card
   * is not empty behind it and says nothing about a failure that has not
   * happened.
   *
   * The assertion this replaces compared the position of the two strings, and
   * `-1 < 0` holds when the checking line is missing entirely: removing the
   * loading branch — the finding's own defect — left it green.
   */
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

    // And the card is more than the support line it used to consist of.
    expect(card.replace(wrapper.find(".help-section").text(), "").trim()).toBe(
      "Проверяем ссылку...",
    );
  });
});
