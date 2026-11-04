/**
 * @vitest-environment jsdom
 */

/**
 * The page the approval letter leads to: the two steps of the choice, the
 * caution on the second, and the dead links, of which only the expired one
 * leaves anything to do.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi, UsernameInput } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import UsernameChangePage from "./UsernameChangePage.vue";
import { push } from "./guestPages.doubles";

vi.mock("vue-router", async () =>
  (await import("./guestPages.doubles")).vueRouter(),
);

// The account refresh after the rename is stubbed: the page only calls it.
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  signIn: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

/** A request that never settles: the page stays in the state under test. */
const pending = () => new Promise(() => {}) as never;

/** A card for a guest or for a reader signed in on this device: who reads decides the way out of an expired link. */
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
  // The hours of an approval link, not the default: the page says what the answer named.
  vi.spyOn(accountApi, "getUsernameChangeApproval").mockResolvedValue({
    data: { status, currentUsername, linkLifetimeHours: 30 },
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

/** The name taken between the check and the send; the approval stands, so the reader picks another. */
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
    // The viewer is persisted: a signed-in fixture would be read back by the next test.
    localStorage.clear();
    // The value arrives in the fragment.
    window.location.hash = "#token=a-token";
    // UsernameInput checks a name after a debounce; answered here, no stray request outlives the test.
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
  });

  /** The two steps of the choice, the caution on the second. */
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

      // The caution stands over the plate; both positions are checked to be real.
      const text = wrapper.text();
      const caution = text.indexOf("Будьте внимательны");
      const plate = text.indexOf("Новое имя");
      expect(caution).toBeGreaterThan(-1);
      expect(plate).toBeGreaterThan(-1);
      expect(caution).toBeLessThan(plate);
    });

    /** The caution says a further change is exceptional, as the activation says it. */
    it("says a further change is exceptional and not merely repeatable", async () => {
      const box = (await atTheConfirmation()).get(".auth-warnbox");

      expect(box.get("b").text()).toBe("Будьте внимательны");
      expect(box.text()).toContain(
        "Дальнейшая смена имени возможна лишь в исключительных случаях",
      );
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
      // The stored viewer has the old name until this runs.
      expect(fetchUser).toHaveBeenCalled();
    });
  });

  /** The finished card: a heading for the fact and one line under it. */
  describe("the card that says the name is changed", () => {
    it("names the fact and confirms it in one line", async () => {
      const wrapper = await atTheChangedName();

      expect(wrapper.get(".auth-title").text()).toBe("Имя изменено");
      expect(wrapper.get(".auth-text").text()).toBe(
        "Ваше имя успешно обновлено",
      );
    });

    it("offers the profile, and offers no help nobody needs", async () => {
      const wrapper = await atTheChangedName();
      const on = wrapper.get(".auth-button");

      expect(on.text()).toBe("Перейти в профиль");
      await on.trigger("click");
      expect(push).toHaveBeenCalledWith("/users/newreader");

      // Nothing has gone wrong, so no help is offered.
      expect(wrapper.find(".auth-footnote").exists()).toBe(false);
    });
  });

  /** The name given away while it was being confirmed: one verdict on screen. */
  describe("the name went to somebody else while it was being confirmed", () => {
    it("takes the reader back to the field and says so once", async () => {
      const wrapper = await atTheTakenName();

      expect(wrapper.get(".auth-title").text()).toBe("Выберите новое имя");
      expect(wrapper.findComponent(UsernameInput).exists()).toBe(true);
      expect(wrapper.get(".username-change-refusal").text()).toBe(
        "Это имя занято",
      );
      // The field's own strip steps aside.
      expect(wrapper.get(".username-change-name__field").classes()).toContain(
        "username-change-name__field--refused",
      );
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

  /** A letter followed twice and a window that closed both come back as a refusal: the token is asked again. */
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
      "Ссылка действительна 30 часов",
    );
    expect(wrapper.get(".auth-note").text()).toBe(
      "Переподайте заявку для изменения имени",
    );
    expect(wrapper.findComponent(UsernameInput).exists()).toBe(false);
  });

  /** A spent approval is the one dead link of the site. */
  it("says nothing of its own about a spent link", async () => {
    const wrapper = await at("used");

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    expect(wrapper.find(".auth-note").exists()).toBe(false);
  });

  /** The broken-link card names the one cause it can have: an address cut, edited, or followed twice. */
  it("calls the link broken and names the one cause it can have", async () => {
    const wrapper = await atBrokenLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    // One line and no second voice.
    expect(wrapper.find(".auth-note").exists()).toBe(false);
  });

  /** An address without the fragment asks the server nothing. */
  it("does not call the server when the address carries no token", async () => {
    window.location.hash = "";
    const check = vi.spyOn(accountApi, "getUsernameChangeApproval");

    const wrapper = mount(UsernameChangePage, options());
    await flushPromises();

    expect(check).not.toHaveBeenCalled();
    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
  });

  /** Every dead end leaves by one button; only the expired link has a way on: the settings, after a sign-in for a guest. */
  describe("the way out of a dead link", () => {
    it.each([
      [
        "the expired",
        () => at("expired", true),
        "Перейти в настройки",
        "/account",
      ],
      ["the spent", () => at("used", true), "На главную", "/"],
      ["the broken", atBrokenLink, "На главную", "/"],
    ] as const)(
      "leaves %s link by one button",
      async (_, open, caption, goes) => {
        const wrapper = await open();
        const ways = wrapper.findAll("button");

        // One control and one only.
        expect(ways).toHaveLength(1);
        expect(ways[0].classes()).toContain("auth-button");
        expect(ways[0].text()).toBe(caption);

        await ways[0].trigger("click");
        expect(push).toHaveBeenCalledWith(goes);
      },
    );

    it("asks a guest on the expired link to sign in, with a button", async () => {
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

    it("asks nobody to sign in on the spent link", async () => {
      const wrapper = await at("used");

      expect(wrapper.get(".auth-button").text()).toBe("На главную");
      expect(wrapper.text()).not.toContain("Войти");
    });

    // The support line stays a link, the only one on the card.
    it.each([
      ["the expired", () => at("expired")],
      ["the spent", () => at("used")],
      ["the broken", atBrokenLink],
    ] as const)(
      "keeps the support link on %s link, the only one",
      async (_, open) => {
        const wrapper = await open();
        const links = wrapper.findAll("a");

        expect(links).toHaveLength(1);
        expect(links[0].text()).toBe("поддержку");
        expect(wrapper.get(".auth-footnote").text()).toContain("Нужна помощь?");
      },
    );
  });
});
