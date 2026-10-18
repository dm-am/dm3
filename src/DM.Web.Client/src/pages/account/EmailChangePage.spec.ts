/**
 * @vitest-environment jsdom
 */

/**
 * The page the address-change letter leads to.
 *
 * It asks nothing of the reader — opening the link is the confirmation — so it
 * has no form. What it has is four answers, and two of them were wrong before
 * this pass: the card that worked said the address had changed without saying
 * what to, and the card that failed printed whatever the server had put in the
 * errors dictionary, which is a code and not a sentence. A reader whose new
 * address had been taken in the meantime was shown the word "Taken".
 *
 * It was also the one of the three pages from letters that still drew its own
 * card, its own width and its own footer, so what is asserted here is both:
 * what it says in each state, and which sheet it asks to be drawn by.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import EmailChangePage from "./EmailChangePage.vue";

const push = vi.fn();

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/**
 * The confirmation carries no body, so the address the account now answers to
 * is read back off the account. That is a session call and not part of what
 * this page decides, so it is stubbed — and the stub is what puts the new
 * address where the page looks for it.
 */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  register: vi.fn(),
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

/**
 * A refusal the way the server sends one: the codes stand in the errors
 * dictionary of the problem document, which is what parseApiErrors reads.
 */
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
    // The viewer is persisted, so a signed-in fixture would otherwise be read
    // back by the guest one standing after it.
    localStorage.clear();
    // The value arrives in the fragment: it is never sent to a server, so it is
    // in no access log and in no Referer.
    window.location.hash = "#token=a-token";
  });

  /**
   * The first frame. The page used to read its state off the `loading` flag of
   * the async helper, which starts false, so the card that was drawn before
   * anything had been asked of the server was the failure card.
   */
  it("says it is checking before the server has answered", () => {
    vi.spyOn(accountApi, "confirmEmailChange").mockImplementation(pending);

    const wrapper = mount(EmailChangePage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
    for (const failure of ["занята", "недействительна", "устарела"]) {
      expect(wrapper.text()).not.toContain(failure);
    }
    // Nothing has gone wrong while the server is being asked, so there is
    // nobody to ask for help either.
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  /**
   * The card that states the outcome. It used to say the address had changed
   * and stop there, which is the one thing the reader already knew: what they
   * came to find out is which address it changed to.
   */
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

    /**
     * The letter can be opened anywhere, and the address is only on the
     * account: a reader who is not signed in on this device is told the fact
     * without the tail, and never "обновлена на" with nothing after it.
     */
    it("stops at the fact when it cannot know the address", async () => {
      const wrapper = await confirmed();

      expect(wrapper.get(".auth-text").text()).toBe(
        "Ваша почта успешно обновлена",
      );
    });
  });

  /**
   * The one refusal that is not a dead link, and the one this page used to
   * answer in the server's own vocabulary.
   */
  describe("the address was taken while the letter waited", () => {
    it("says so in words and not in the code the server sent", async () => {
      const wrapper = await takenAddress();

      // The heading names the outcome, the way the rest of the surface does.
      // It used to say "Ошибка подтверждения", which fits every failed link on
      // the site and tells the reader nothing about the one he is looking at.
      expect(wrapper.get(".auth-title").text()).toBe("Почта занята");
      expect(wrapper.get(".auth-text").text()).toBe(
        "Этот адрес уже занят другим аккаунтом",
      );
      expect(wrapper.get(".auth-note").text()).toBe(
        "Запросите смену почты на другой адрес",
      );
      expect(wrapper.text()).not.toContain("Taken");
      expect(wrapper.text()).not.toContain("Уже занято");
    });
  });

  /**
   * Every other refusal is the link itself, and the sentence names which link:
   * a reader may be holding three of them from the same site.
   */
  it("names the link when the link is what is dead", async () => {
    const wrapper = await refused();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
  });

  /**
   * The codes are the contract; the wording is this page's job. A code it has
   * no card for is still not something to print at the reader.
   */
  it("prints no code the server sends it", async () => {
    const wrapper = await refused({ token: ["Invalid"] });

    expect(wrapper.text()).not.toContain("Invalid");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
  });

  /**
   * An address opened without the fragment — a bookmark, a copy that lost the
   * tail. Nothing is asked of the server for it, and nothing is invented to
   * explain it: the page had one word for the missing value and it was the name
   * of the thing in the database.
   */
  it("asks nobody about an address that carries no token", async () => {
    window.location.hash = "";
    const confirm = vi.spyOn(accountApi, "confirmEmailChange");

    const wrapper = mount(EmailChangePage, options());
    await flushPromises();

    expect(confirm).not.toHaveBeenCalled();
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    expect(wrapper.text()).not.toContain("Токен");
  });

  it.each([
    ["адрес занят", takenAddress],
    ["ссылка недействительна", () => refused()],
  ] as const)("выводит с карточки %s кнопкой", async (_, open) => {
    const wrapper = await open();
    const out = wrapper.get(".auth-button");

    expect(out.element.tagName).toBe("BUTTON");
    expect(out.text()).toBe("На главную");

    await out.trigger("click");
    expect(push).toHaveBeenCalledWith("/");
  });

  /**
   * One card for a finished change, whoever is reading it. The page used to
   * read who was signed in and hand the guest a second control beside the
   * first, so one outcome was drawn as two different cards. Owner's decision.
   */
  it.each([
    ["вошедшего", () => signedInAs("reader@dm.am")],
    ["гостя", () => {}],
  ] as const)("выводит %s одной кнопкой на главную", async (_, sign) => {
    sign();
    const wrapper = await confirmed();

    const ways = wrapper.findAll("button");
    expect(ways.map((button) => button.text())).toEqual(["На главную"]);
    expect(ways[0].classes()).toContain("auth-button");
    expect(wrapper.find(".form-actions").exists()).toBe(false);

    await ways[0].trigger("click");
    expect(push).toHaveBeenCalledWith("/");
  });

  /**
   * The sheet the card asks to be drawn by. The page carried a card, a width
   * and a footer of its own; the surface owns all three, and a rule written
   * here again drifts from the two pages beside it the moment either is tuned.
   */
  it.each([
    ["почта изменена", confirmed],
    ["адрес занят", takenAddress],
    ["ссылка недействительна", () => refused()],
  ] as const)("рисует карточку %s гостевой поверхностью", async (_, open) => {
    const wrapper = await open();
    const card = wrapper.get(".auth-card");

    expect(wrapper.get(".auth-surface")).toBeTruthy();
    expect(card.classes()).toContain("auth-card--framed");
    expect(card.classes()).toContain("auth-card--center");
    // A mark, a heading and one line: the narrow measure, as on the other pages
    // a letter opens.
    expect(card.classes()).toContain("auth-card--compact");
    expect(wrapper.get("h1").classes()).toContain("auth-title");

    // The card and the voices the page kept for itself.
    expect(wrapper.find(".confirm-card").exists()).toBe(false);
    expect(wrapper.find(".status-description").exists()).toBe(false);
    expect(wrapper.find(".error-text").exists()).toBe(false);
  });

  it.each([
    ["ссылка недействительна", () => refused()],
    ["адрес занят", takenAddress],
  ] as const)("зовет в поддержку с карточки %s", async (_, open) => {
    const wrapper = await open();
    const footnote = wrapper.get(".auth-footnote");

    expect(footnote.text()).toContain("Нужна помощь?");
    expect(footnote.get("a").text()).toBe("поддержку");
    // And it is the only link left on the card.
    expect(wrapper.findAll("a")).toHaveLength(1);
  });
});
