/**
 * @vitest-environment jsdom
 */

/**
 * The page the address-change letter leads to, in each of its four answers:
 * what it says, the address it names when it knows it, and no code of the server.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import EmailChangePage from "./EmailChangePage.vue";
import { push } from "./guestPages.doubles";

vi.mock("vue-router", async () =>
  (await import("./guestPages.doubles")).vueRouter(),
);

/** The account refresh is stubbed; the stub puts the new address where the page reads it. */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  signIn: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

/** A request that never settles: the page stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

const options = () => {
  const pinia = createPinia();
  setActivePinia(pinia);

  return {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  };
};

/** A reader who followed the letter while signed in on this device. */
const signedInAs = (email: string) =>
  fetchUser.mockImplementation(() => {
    useAuthStore().updateUser({ username: "reader", email } as never);
  });

const confirmed = async () => {
  vi.spyOn(accountApi, "confirmEmailChange").mockResolvedValue({
    data: {},
    error: null,
  } as never);

  const wrapper = mount(EmailChangePage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

/** A refusal as the server sends one, the codes in the errors of the problem document. */
const refused = async (errors?: Record<string, string[]>) => {
  vi.spyOn(accountApi, "confirmEmailChange").mockResolvedValue({
    data: null,
    error: { status: errors ? 400 : 404, title: "Gone", errors },
  } as never);

  const wrapper = mount(EmailChangePage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

/** The address went to somebody else while the letter waited. */
const takenAddress = () => refused({ email: ["Taken"] });

describe("the page reached from the address-change letter", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    push.mockClear();
    fetchUser.mockReset();
    // The viewer is persisted: a signed-in fixture would be read back by the next test.
    localStorage.clear();
    // The value arrives in the fragment.
    window.location.hash = "#token=a-token";
  });

  /** The first frame says it is checking. */
  it("says it is checking before the server has answered", () => {
    vi.spyOn(accountApi, "confirmEmailChange").mockImplementation(pending);

    const wrapper = mount(EmailChangePage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
    // Nothing has gone wrong yet, so no help is offered.
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  /** The finished card names the address it changed to. */
  describe("the card that says the address is changed", () => {
    it("names the address the account now answers to", async () => {
      signedInAs("lexx@dm.am");
      const confirm = vi.spyOn(accountApi, "confirmEmailChange");
      const wrapper = await confirmed();

      expect(confirm).toHaveBeenCalledWith("a-token");
      expect(wrapper.get(".auth-title").text()).toBe("Почта изменена");
      expect(wrapper.get(".auth-text").text()).toBe(
        "Ваша почта успешно обновлена на lexx@dm.am",
      );
      expect(wrapper.find(".auth-footnote").exists()).toBe(false);
    });

    /** A reader not signed in on this device is told the fact without the address. */
    it("stops at the fact when it cannot know the address", async () => {
      const wrapper = await confirmed();

      expect(wrapper.get(".auth-text").text()).toBe(
        "Ваша почта успешно обновлена",
      );
    });
  });

  /** The one refusal that is not a dead link, worded by the page. */
  describe("the address was taken while the letter waited", () => {
    it("says so in words and not in the code the server sent", async () => {
      const wrapper = await takenAddress();

      // The heading names the outcome.
      expect(wrapper.get(".auth-title").text()).toBe("Почта занята");
      expect(wrapper.get(".auth-text").text()).toBe(
        "Этот адрес уже занят другим аккаунтом",
      );
      expect(wrapper.get(".auth-note").text()).toBe(
        "Запросите смену почты на другой адрес",
      );
      expect(wrapper.text()).not.toContain("Taken");
    });
  });

  /** Every other refusal is the link, and the sentence names which link. */
  it("names the link when the link is what is dead", async () => {
    const wrapper = await refused();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
  });

  /** A code without a card of its own is not printed either. */
  it("prints no code the server sends it", async () => {
    const wrapper = await refused({ token: ["Invalid"] });

    expect(wrapper.text()).not.toContain("Invalid");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
  });

  /** An address without the fragment asks the server nothing. */
  it("asks nobody about an address that carries no token", async () => {
    window.location.hash = "";
    const confirm = vi.spyOn(accountApi, "confirmEmailChange");

    const wrapper = mount(EmailChangePage, options());
    await flushPromises();

    expect(confirm).not.toHaveBeenCalled();
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
  });

  /** One way home from every answer, whoever reads it. */
  it.each([
    ["the taken address", takenAddress],
    ["the dead link", () => refused()],
    [
      "the changed address, signed in",
      () => {
        signedInAs("reader@dm.am");
        return confirmed();
      },
    ],
    ["the changed address, as a guest", confirmed],
  ] as const)("leads home from %s with one button", async (_, open) => {
    const wrapper = await open();

    const ways = wrapper.findAll("button");
    expect(ways.map((button) => button.text())).toEqual(["На главную"]);

    await ways[0].trigger("click");
    expect(push).toHaveBeenCalledWith("/");
  });

  /** The support line on the two answers that need it. */
  it.each([
    ["the dead link", () => refused()],
    ["the taken address", takenAddress],
  ] as const)("calls for support from %s", async (_, open) => {
    const wrapper = await open();
    const footnote = wrapper.get(".auth-footnote");

    expect(footnote.text()).toContain("Нужна помощь?");
    expect(footnote.get("a").text()).toBe("поддержку");
    // The only link on the card.
    expect(wrapper.findAll("a")).toHaveLength(1);
  });
});
