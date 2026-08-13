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
import { flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, UsernameInput } from "@/entities/user";
import AccountActivationPage from "./AccountActivationPage.vue";
import PasswordResetPage from "./PasswordResetPage.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

// The value arrives in the fragment rather than in the path: a fragment is never
// sent to a server, so it is in no access log and in no Referer. The pages read it
// through readConfirmationToken, which also clears it from the address.
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

  /**
   * The control next to the chosen name on the confirmation step. It goes back
   * a step and nowhere else, so it is a button; as an anchor pointing at "#"
   * with a class that dimmed it, it was still a link to the keyboard, and
   * Enter on it dropped the reader back to step one while the activation
   * request was already on the wire.
   */
  describe("the step-back control on the confirmation step", () => {
    const atConfirmStep = async () => {
      vi.spyOn(accountApi, "getActivationInfo").mockResolvedValue({
        data: { status: "ready", email: "reader@example.com" },
        error: null,
      });

      const wrapper = mount(AccountActivationPage, options());
      await flushPromises();

      const name = wrapper.findComponent(UsernameInput);
      name.vm.$emit("update:modelValue", "reader");
      name.vm.$emit("availability", true);
      await nextTick();
      await wrapper.get(".submit-button").trigger("click");

      expect(wrapper.text()).toContain("Подтвердите выбор имени");
      return wrapper;
    };

    it("is a button, and takes the reader back to the name", async () => {
      const wrapper = await atConfirmStep();
      const back = wrapper.get(".change-username");

      expect(back.element.tagName).toBe("BUTTON");
      expect(back.attributes("disabled")).toBeUndefined();

      await back.trigger("click");

      expect(wrapper.text()).toContain("Выберите имя");
    });

    it("is disabled, and not merely dimmed, while the request is in flight", async () => {
      const wrapper = await atConfirmStep();
      vi.spyOn(accountApi, "activate").mockImplementation(pending);

      await wrapper.get(".submit-button").trigger("click");

      expect(wrapper.get(".change-username").attributes("disabled")).toBe("");
      expect(wrapper.text()).toContain("Подтвердите выбор имени");
    });
  });
});
