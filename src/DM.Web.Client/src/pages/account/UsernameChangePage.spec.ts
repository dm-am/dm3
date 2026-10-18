/**
 * @vitest-environment jsdom
 */

/**
 * The page the approval letter leads to.
 *
 * The letter pointed at an address the site does not route, so the last step of
 * the name change was a 404 and the approval could only run out. What the page
 * owes the reader is not one form but five answers: two steps of the choice,
 * and three ways a link can be dead, which are different events with only one
 * of them leaving anything to do.
 *
 * The choice is made in two steps, the way the activation makes it, and the
 * caution about a further change belongs to the second one: on the first the
 * reader is still trying names out, and there is nothing yet to be cautious
 * about. Both halves of that are asserted here, because "the box moved" is only
 * true if it is gone from where it was.
 *
 * It owes the reader all of it on the guest surface. The page drew a card, a
 * width and a footer of its own until the three pages a letter opens were put
 * side by side, so the classes it wears are asserted as well: nothing mounted
 * in jsdom can be asked what the card looks like, but it can be asked which
 * sheet it is asking to be drawn by.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, UsernameInput } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import UsernameChangePage from "./UsernameChangePage.vue";

const push = vi.fn();

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

// fetchUser reconciles the viewer with the server after the rename. It is a
// session call, not part of what this page decides, and it is stubbed so the
// success branch does not depend on a signed-in viewer.
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

/**
 * A card mounted for a guest, or for a reader signed in on this device. Who is
 * reading decides the way out of an expired link, so the viewer is part of the
 * fixture and not a global.
 */
const options = (signedIn = false) => {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (signedIn) useAuthStore().updateUser({ username: "reader" } as never);

  return {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { template: "<a><slot /></a>" } },
    },
  };
};

const approval = (
  status: "ready" | "expired" | "used",
  currentUsername?: string,
) =>
  vi.spyOn(accountApi, "getUsernameChangeApproval").mockResolvedValue({
    data: { status, currentUsername },
    error: null,
  } as never);

/** Step one: a live approval, with a name typed into the field. */
const atTheName = async (name = "newreader") => {
  approval("ready", "reader");

  const wrapper = mount(UsernameChangePage, options());
  await flushPromises();

  const field = wrapper.findComponent(UsernameInput);
  field.vm.$emit("update:modelValue", name);
  field.vm.$emit("availability", true);
  await nextTick();

  return wrapper as VueWrapper;
};

/** Step two: the name is typed and the reader has gone on to confirm it. */
const atTheConfirmation = async (name = "newreader") => {
  const wrapper = await atTheName(name);
  await wrapper.get("form").trigger("submit");
  return wrapper;
};

/** Mounts the page on the answer the approval token gives. */
const at = async (status: "expired" | "used", signedIn = false) => {
  approval(status);

  const wrapper = mount(UsernameChangePage, options(signedIn));
  await flushPromises();
  return wrapper as VueWrapper;
};

/** The page with a token nothing was ever issued for. */
const atBrokenLink = async () => {
  vi.spyOn(accountApi, "getUsernameChangeApproval").mockResolvedValue({
    data: null,
    error: { status: 404, title: "Ссылка недействительна или устарела" },
  } as never);

  const wrapper = mount(UsernameChangePage, options());
  await flushPromises();
  return wrapper as VueWrapper;
};

/** The page after the name has been changed. */
const atTheChangedName = async () => {
  vi.spyOn(accountApi, "completeUsernameChange").mockResolvedValue({
    data: { requestedUsername: "newreader" },
    error: null,
  } as never);

  const wrapper = await atTheConfirmation();
  await wrapper.get(".auth-button").trigger("click");
  await flushPromises();
  return wrapper;
};

/**
 * The name was given away between the check in the field and the send. The
 * approval itself still stands, which is what the second reading of the token
 * says, so the reader keeps it and picks another name.
 */
const atTheTakenName = async () => {
  const wrapper = await atTheConfirmation();

  vi.spyOn(accountApi, "completeUsernameChange").mockResolvedValue({
    data: null,
    error: { status: 409, title: "Это имя уже занято" },
  } as never);
  approval("ready", "reader");

  await wrapper.get(".auth-button").trigger("click");
  await flushPromises();
  return wrapper;
};

describe("the page reached from the approval letter", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    push.mockClear();
    fetchUser.mockClear();
    // The viewer is persisted, so a signed-in fixture would otherwise be read
    // back by the guest one standing after it.
    localStorage.clear();
    // The value arrives in the fragment: it is never sent to a server, so it is
    // in no access log and in no Referer.
    window.location.hash = "#token=a-token";
    // UsernameInput checks the name it is given after a debounce. Left alone it
    // reaches for the network once the assertions are already done, and jsdom
    // reports the stray request as an unhandled error against whichever test is
    // still open.
    vi.spyOn(accountApi, "checkUsername").mockResolvedValue({
      data: { available: true },
      error: null,
    } as never);
  });

  it("tells the reader the link is being checked", () => {
    vi.spyOn(accountApi, "getUsernameChangeApproval").mockImplementation(
      pending,
    );

    const wrapper = mount(UsernameChangePage, options());

    expect(wrapper.text()).toContain("Проверяем ссылку...");
    // And says nothing yet about a failure that has not happened.
    for (const failure of ["устарела", "уже изменено", "недействительна"]) {
      expect(wrapper.text()).not.toContain(failure);
    }
  });

  /**
   * The two steps of the choice. The caution used to stand on the first one,
   * over a field the reader had not filled in yet.
   */
  describe("the name is chosen in two steps", () => {
    it("asks for the name first, and cautions nobody about it", async () => {
      const wrapper = await atTheName();

      expect(wrapper.get(".auth-title").text()).toBe("Выберите новое имя");
      expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
      expect(wrapper.find(".auth-warnbox").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("Будьте внимательны");
      expect(wrapper.get(".auth-button").text()).toBe("Продолжить");
    });

    it("repeats the name back on the second step, under the caution", async () => {
      const wrapper = await atTheConfirmation();

      expect(wrapper.get(".auth-title").text()).toBe("Подтвердите выбор имени");
      expect(wrapper.get(".auth-value").text()).toBe("newreader");
      expect(wrapper.find("form").exists()).toBe(false);
      expect(wrapper.get(".auth-button").text()).toBe("Сменить имя");

      // The box stands over the plate and not under it: it is read before the
      // button, not after. Both positions are asserted to be real ones —
      // `-1 < 0` holds when the caution is missing entirely, and a comparison
      // alone would pass on a card that lost it.
      const text = wrapper.text();
      const caution = text.indexOf("Будьте внимательны");
      const plate = text.indexOf("Новое имя");
      expect(caution).toBeGreaterThan(-1);
      expect(plate).toBeGreaterThan(-1);
      expect(caution).toBeLessThan(plate);
    });

    /**
     * What the caution says. The page promised the reader another application
     * and another moderator, which is not what happens: a second change is
     * refused unless the case is exceptional, and that is what the confirmation
     * step of the activation has been telling them all along.
     */
    it("says a further change is exceptional and not merely repeatable", async () => {
      const box = (await atTheConfirmation()).get(".auth-warnbox");

      expect(box.get("b").text()).toBe("Будьте внимательны");
      expect(box.text()).toContain(
        "Дальнейшая смена имени возможна лишь в исключительных случаях",
      );
      expect(box.text()).not.toContain("одобрения модератора");
    });

    it("goes back to the field from the confirmation", async () => {
      const wrapper = await atTheConfirmation();

      await wrapper.get(".auth-action").trigger("click");

      expect(wrapper.get(".auth-title").text()).toBe("Выберите новое имя");
      expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
    });

    it("sends the chosen name off the approval token", async () => {
      const complete = vi
        .spyOn(accountApi, "completeUsernameChange")
        .mockResolvedValue({
          data: { requestedUsername: "newreader" },
          error: null,
        } as never);

      await atTheChangedName();

      expect(complete).toHaveBeenCalledWith("a-token", "newreader");
      // The stored viewer still carries the old name until this runs.
      expect(fetchUser).toHaveBeenCalled();
    });
  });

  /**
   * The card that states the outcome. It says the news the way the address
   * change says it: a heading for the fact and one line under it.
   */
  describe("the card that says the name is changed", () => {
    it("names the fact and confirms it in one line", async () => {
      const wrapper = await atTheChangedName();

      expect(wrapper.get(".auth-title").text()).toBe("Имя изменено");
      expect(wrapper.get(".auth-text").text()).toBe(
        "Ваше имя успешно обновлено",
      );
      expect(wrapper.text()).not.toContain("Имя успешно изменено");
      expect(wrapper.text()).not.toContain("Теперь вас зовут");
    });

    it("offers the profile, and offers no help nobody needs", async () => {
      const wrapper = await atTheChangedName();
      const on = wrapper.get(".auth-button");

      expect(on.text()).toBe("Перейти в профиль");
      await on.trigger("click");
      expect(push).toHaveBeenCalledWith("/users/newreader");

      // Nothing has gone wrong here, so there is nobody to ask for help.
      expect(wrapper.find(".auth-footnote").exists()).toBe(false);
    });
  });

  /**
   * Approval and choice are days apart, so the name can be gone in between.
   * That is the only way the server refuses a name this page has already had
   * checked, and the card used to answer it with a second verdict standing next
   * to the field's own: a green "Имя свободно" over a red refusal.
   */
  describe("the name went to somebody else while it was being confirmed", () => {
    it("takes the reader back to the field and says so once", async () => {
      const wrapper = await atTheTakenName();

      expect(wrapper.get(".auth-title").text()).toBe("Выберите новое имя");
      expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
      expect(wrapper.get(".username-change-refusal").text()).toBe(
        "Это имя занято",
      );
      // The field's own strip is standing on a question that is no longer being
      // asked, so it steps aside rather than contradicting the answer.
      expect(wrapper.get(".username-change-name__field").classes()).toContain(
        "username-change-name__field--refused",
      );
    });

    it("has one wording for it and not three", async () => {
      const wrapper = await atTheTakenName();

      for (const retired of [
        "Это имя уже занято",
        "Имя уже занято",
        "Имя зарезервировано",
        "Уже занято",
        "Это имя уже используется",
      ]) {
        expect(wrapper.text()).not.toContain(retired);
      }
    });

    it("does not let the same name through a second time", async () => {
      const wrapper = await atTheTakenName();

      expect(wrapper.get(".auth-button").attributes("disabled")).toBeDefined();
    });

    it("gives the field its verdict back the moment the reader types", async () => {
      const wrapper = await atTheTakenName();

      wrapper.findComponent(UsernameInput).vm.$emit("update:modelValue", "n");
      await nextTick();

      expect(wrapper.find(".username-change-refusal").exists()).toBe(false);
      expect(
        wrapper.get(".username-change-name__field").classes(),
      ).not.toContain("username-change-name__field--refused");
    });
  });

  /**
   * The same letter followed in two tabs, and a window that closed while the
   * confirmation was open. Both arrive as a refusal on the same call — the
   * first is a 409, exactly like a taken name — so what separates them is the
   * token being asked again, not the sentence the server sent.
   */
  it.each([
    ["used", 409, "Ссылка недействительна"],
    ["expired", 404, "Ссылка устарела"],
  ] as const)(
    "leaves the choice for the %s link the second reading reports",
    async (status, httpStatus, shown) => {
      const wrapper = await atTheConfirmation();

      vi.spyOn(accountApi, "completeUsernameChange").mockResolvedValue({
        data: null,
        error: { status: httpStatus, title: "anything at all" },
      } as never);
      approval(status);

      await wrapper.get(".auth-button").trigger("click");
      await flushPromises();

      expect(wrapper.get(".auth-title").text()).toBe(shown);
      expect(wrapper.findComponent(UsernameInput).exists()).toBe(false);
      expect(wrapper.find(".auth-value").exists()).toBe(false);
    },
  );

  it("offers a new request when the approval ran out before the page opened", async () => {
    const wrapper = await at("expired");

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка устарела");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Ссылка действительна 48 часов",
    );
    expect(wrapper.get(".auth-note").text()).toBe(
      "Переподайте заявку для изменения имени",
    );
    expect(wrapper.findComponent(UsernameInput).exists()).toBe(false);
  });

  /**
   * A spent approval is a dead link and nothing else: nothing is owed to the
   * reader and there is nothing left to do with it. It had a card of its own
   * ("Имя уже изменено"), which is a sixth name for the answer the other five
   * guest screens give in one. Owner's decision.
   */
  it("says nothing of its own about a spent link", async () => {
    const wrapper = await at("used");

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    expect(wrapper.find(".auth-note").exists()).toBe(false);
    for (const retired of ["Имя уже изменено", "Новое имя уже было выбрано"]) {
      expect(wrapper.text()).not.toContain(retired);
    }
  });

  /**
   * What actually puts a reader on the broken-link card, which is not what the
   * card used to claim. A rejected request is never given a token and never
   * mails a link, so a moderator's refusal cannot be read here; expiry has a
   * card of its own, because there the reader can still file a new request.
   * What is left is the address — a copy that lost its tail, a value the mail
   * client cut, an address edited by hand — and the same letter followed twice.
   */
  it("calls the link broken and names the one cause it can have", async () => {
    const wrapper = await atBrokenLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    // One line and no second voice: the causes the two-line version guessed at
    // (a rejected request, a stale copy) either never issue a link or have a
    // screen of their own.
    expect(wrapper.find(".auth-note").exists()).toBe(false);

    for (const retired of [
      "Ссылка не работает",
      "заявку отклонил модератор",
      "последнего письма",
    ]) {
      expect(wrapper.text()).not.toContain(retired);
    }
  });

  /**
   * An address opened without the fragment — a bookmark, a copy that lost the
   * tail. Nothing is asked of the server for it.
   */
  it("does not call the server when the address carries no token", async () => {
    window.location.hash = "";
    const check = vi.spyOn(accountApi, "getUsernameChangeApproval");

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(check).not.toHaveBeenCalled();
    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
  });

  /**
   * Every dead end leaves by a button. Which button depends on what is left to
   * do there, and only the expired link has anything: the way on is a new
   * request, and a new request is filed from the settings — so a reader who is
   * not signed in is sent to sign in first. On a spent link there is nothing to
   * come back for, and the card that used to send the reader to a login form
   * was sending them to sign in as somebody whose name is already changed.
   */
  describe("the way out of a dead link", () => {
    it.each([
      [
        "устаревшей",
        () => at("expired", true),
        "Перейти в настройки",
        "/account",
      ],
      ["использованной", () => at("used", true), "На главную", "/"],
      ["сломанной", atBrokenLink, "На главную", "/"],
    ] as const)("на %s ссылке — кнопка", async (_, open, caption, goes) => {
      const wrapper = await open();
      const ways = wrapper.findAll("button");

      // One control and one only: a dead link leaves the reader nothing to
      // choose between.
      expect(ways).toHaveLength(1);
      expect(ways[0].classes()).toContain("auth-button");
      expect(ways[0].text()).toBe(caption);

      await ways[0].trigger("click");
      expect(push).toHaveBeenCalledWith(goes);
    });

    it("на устаревшей ссылке зовет гостя войти — кнопкой", async () => {
      const wrapper = await at("expired");
      const out = wrapper.get(".auth-button");

      expect(out.element.tagName).toBe("BUTTON");
      expect(out.text()).toBe("Войти");

      await out.trigger("click");
      expect(push).toHaveBeenCalledWith({
        path: "/",
        query: { action: "login" },
      });
    });

    it("на использованной ссылке не зовет войти никого", async () => {
      const wrapper = await at("used");

      expect(wrapper.get(".auth-button").text()).toBe("На главную");
      expect(wrapper.text()).not.toContain("Войти");
    });

    // The support line stays a link, and it is the only one left on the card.
    it.each([
      ["устаревшей", () => at("expired")],
      ["использованной", () => at("used")],
      ["сломанной", atBrokenLink],
    ] as const)("на %s ссылке не прячет ссылку в текст", async (_, open) => {
      const wrapper = await open();
      const links = wrapper.findAll("a");

      expect(links).toHaveLength(1);
      expect(links[0].text()).toBe("поддержку");
      expect(wrapper.get(".auth-footnote").text()).toContain("Нужна помощь?");
    });
  });

  /**
   * The sheet the card asks to be drawn by. The page carried a card, a width
   * and a footer of its own; the surface owns all three, and a rule written
   * here again drifts from the two pages beside it the moment either is tuned.
   */
  describe("the card of this page is the guest surface's", () => {
    const everyState: readonly [string, () => Promise<VueWrapper>][] = [
      ["выбор имени", atTheName],
      ["подтверждение имени", atTheConfirmation],
      ["имя изменено", atTheChangedName],
      ["ссылка устарела", () => at("expired")],
      ["ссылка недействительна на использованной", () => at("used")],
      ["ссылка недействительна", atBrokenLink],
    ];

    it.each(everyState)("на карточке %s", async (_, open) => {
      const wrapper = await open();
      const card = wrapper.get(".auth-card");

      expect(wrapper.get(".auth-surface")).toBeTruthy();
      expect(card.classes()).toContain("auth-card--framed");
      expect(card.classes()).toContain("auth-card--center");
      // Narrow in every state, as the activation is: one field at most, and a
      // heading with a line or two under it.
      expect(card.classes()).toContain("auth-card--compact");
      // The card the page used to draw for itself, and the wide measure it
      // reached for on the broken link.
      expect(wrapper.find(".username-change-card").exists()).toBe(false);
      expect(wrapper.find(".username-change-page--wide").exists()).toBe(false);
    });

    it.each(everyState)(
      "заголовком поверхности на карточке %s",
      async (_, open) => {
        const wrapper = await open();

        expect(wrapper.get("h1").classes()).toContain("auth-title");
        // The voices the page kept for itself.
        expect(wrapper.find(".status-description").exists()).toBe(false);
        expect(wrapper.find(".status-note").exists()).toBe(false);
      },
    );
  });
});
