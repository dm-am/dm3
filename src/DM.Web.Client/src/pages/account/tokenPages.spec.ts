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

  it("does not offer support while nothing has gone wrong yet", () => {
    // The support line is the whole card when every branch is false, and that
    // is exactly what the reader used to get.
    vi.spyOn(accountApi, "getActivationInfo").mockImplementation(pending);

    const wrapper = mount(AccountActivationPage, options());
    const text = wrapper.text();

    expect(text.indexOf("Проверяем ссылку...")).toBeLessThan(
      text.indexOf("Нужна помощь?"),
    );
  });
});
