/**
 * @vitest-environment jsdom
 */

/**
 * The page the activation letter leads to: which card each answer stands on,
 * which offers help, one attempt at a new letter, the caret in the one field,
 * and no sentence the server did not send. The checking line is tokenPages.spec.ts's.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { Api } from "@/shared/api";
import { accountApi, UsernameInput } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import AccountActivationPage from "./AccountActivationPage.vue";
import { push } from "./guestPages.doubles";

vi.mock("vue-router", async () =>
  (await import("./guestPages.doubles")).vueRouter(),
);

// The token arrives in the fragment; readConfirmationToken takes it from there.
beforeEach(() => {
  window.location.hash = "#token=a-token";
});

const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
});

const ready = {
  data: { status: "ready" as const, email: "reader@example.com" },
  error: null,
};
const expired = {
  data: { status: "expired" as const, email: "reader@example.com" },
  error: null,
};

/** Land on the page with the answer of the check; `attach` puts it in the document, for the caret. */
const arrive = async (info: unknown, attach = false) => {
  vi.spyOn(accountApi, "getActivationInfo").mockResolvedValue(
    info as Awaited<ReturnType<typeof accountApi.getActivationInfo>>,
  );

  const wrapper = mount(AccountActivationPage, {
    ...options(),
    ...(attach ? { attachTo: document.body } : {}),
  });
  await flushPromises();
  return wrapper;
};

/** The channel of the two tabs, handing over the instance the page opened and what it posted. */
class TabChannel {
  static last: TabChannel | null = null;
  onmessage: ((event: { data: unknown }) => void) | null = null;
  readonly posted: unknown[] = [];

  constructor(readonly name: string) {
    TabChannel.last = this;
  }

  postMessage(data: unknown) {
    this.posted.push(data);
  }

  close() {}
}

/** Open the page as the second tab, with its channel under the test's hand. */
const withTabs = () => {
  vi.stubGlobal("BroadcastChannel", TabChannel);
};

/** The channel the page opened on mount. */
const openedChannel = () => {
  if (!TabChannel.last) throw new Error("the page opened no channel");
  return TabChannel.last;
};

/** What the other tab says when it has finished the activation. */
const activatedElsewhere = (username = "reader") => {
  openedChannel().onmessage?.({ data: { type: "activated", username } });
  return nextTick();
};

/** The field reports a free name. */
const chooseName = async (wrapper: VueWrapper) => {
  const name = wrapper.findComponent(UsernameInput);
  name.vm.$emit("update:modelValue", "reader");
  name.vm.$emit("availability", true);
  await nextTick();
};

/** Choose a free name and step to the confirmation. */
const atConfirmStep = async () => {
  const wrapper = await arrive(ready);
  await chooseName(wrapper);
  await wrapper.get(".auth-button").trigger("click");
  return wrapper;
};

/** The server's answer to an activation that went through. */
const activated = () =>
  ({
    data: { resource: { id: "1", username: "reader" } as User },
    error: null,
  }) as Awaited<ReturnType<typeof accountApi.activate>>;

describe("the activation page", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    vi.restoreAllMocks();
    // Not a spy, so restoreAllMocks does not reach it.
    push.mockClear();
    // A card left in the document would answer the next test's selectors.
    document.body.innerHTML = "";
  });

  /** Every answer stands on one framed card of one width. */
  it.each([
    ["the choice of the name", ready],
    ["the expired link", expired],
  ])("stands %s on one framed guest card", async (_, info) => {
    const wrapper = await arrive(info);

    expect(
      wrapper
        .find(".auth-surface > .auth-card--framed.auth-card--center")
        .exists(),
    ).toBe(true);
  });

  it("draws the name as a guest field on the same card", async () => {
    const wrapper = await arrive(ready);

    expect(wrapper.text()).toContain("Выберите имя");
    expect(wrapper.get("label[for='activation-username']").text()).toBe(
      "Имя пользователя",
    );
    expect(wrapper.findComponent(UsernameInput).classes()).toContain(
      "auth-field",
    );
  });

  /** The caret is in the one field as soon as the check has answered. */
  it("puts the caret in the name field as soon as it is on screen", async () => {
    const wrapper = await arrive(ready, true);

    expect(document.activeElement).toBe(
      wrapper.get("#activation-username").element,
    );

    wrapper.unmount();
  });

  it("keeps Продолжить disabled until the name is free", async () => {
    const wrapper = await arrive(ready);
    expect(wrapper.get(".auth-button").attributes("disabled")).toBe("");

    await chooseName(wrapper);

    expect(wrapper.get(".auth-button").attributes("disabled")).toBeUndefined();
  });

  /** The name is final, so the step says so in the guest surface's frame and shows the name in a plate. */
  it("warns that the name is final and shows it in a plate", async () => {
    const wrapper = await atConfirmStep();

    expect(wrapper.text()).toContain("Подтвердите выбор имени");
    const warning = wrapper.get(".auth-warnbox");
    expect(warning.text()).toContain("Будьте внимательны");
    expect(warning.text()).toContain(
      "Дальнейшая смена имени возможна лишь в исключительных случаях",
    );
    expect(wrapper.get(".auth-value").text()).toBe("reader");
    expect(wrapper.get(".auth-button").text()).toBe("Завершить регистрацию");
  });

  // The account starts in the zone of the device it is finished on.
  it("sends the time zone of the device with the name", async () => {
    const wrapper = await atConfirmStep();
    const activate = vi
      .spyOn(accountApi, "activate")
      .mockResolvedValue(activated());

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(activate).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
      }),
    );
  });

  it("leaves the finished registration without the line about support", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue(activated());

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Регистрация завершена");
    expect(wrapper.get(".auth-text").text()).toBe("Добро пожаловать, reader");
    expect(wrapper.get("button.auth-button").text()).toBe("Перейти в профиль");
    expect(wrapper.get(".auth-status").classes()).toContain("auth-status--ok");
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  /**
   * One attempt: the offer is replaced by the receipt the registration ends on,
   * with no mark, no button and no offer of help.
   */
  it("replaces the offer of a letter with the receipt for it", async () => {
    const wrapper = await arrive(expired);

    // The heading of every expired link.
    expect(wrapper.get(".auth-title").text()).toBe("Ссылка устарела");
    expect(wrapper.text()).toContain(
      "Отправить новую ссылку для активации на reader@example.com?",
    );
    expect(wrapper.text()).toContain("Ссылка действительна 48 часов");
    expect(wrapper.get(".auth-status").classes()).toContain(
      "auth-status--warn",
    );

    // Before the letter goes out the offer says the fallback; the receipt says what the answer named.
    vi.spyOn(accountApi, "recover").mockResolvedValue({
      data: {
        status: "ActivationResent",
        email: "reader@example.com",
        linkLifetimeHours: 30,
      },
      error: null,
    } as Awaited<ReturnType<typeof accountApi.recover>>);
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.get(".auth-title").text()).toBe("Проверьте почту");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Мы отправили повторное письмо со ссылкой для активации на reader@example.com",
    );
    expect(wrapper.get(".auth-note").text()).toBe(
      "Ссылка действительна 30 часов",
    );
    expect(wrapper.text()).not.toContain("устарела");
    expect(wrapper.find(".auth-status").exists()).toBe(false);
    expect(wrapper.find(".auth-button").exists()).toBe(false);
    expect(wrapper.find(".auth-footnote").exists()).toBe(false);
  });

  /**
   * Who speaks when the second letter does not go out: the card has no strip for
   * it, so the press carries no flags and the interceptor speaks. Followed down
   * to the client, where the flags would be.
   */
  describe("the second letter", () => {
    const { toasts, dismiss } = useToast();

    /** As much of a request's options as the rule here is about. */
    type Flags = { ownsRefusal?: boolean; ownsRateLimit?: boolean };

    beforeEach(() => {
      toasts.value.forEach((raised) => dismiss(raised.id));
    });

    /** The one button of the expired card, pressed and waited out. */
    const askForTheLetter = async () => {
      const wrapper = await arrive(expired);
      await wrapper.get(".auth-button").trigger("click");
      await flushPromises();
      return wrapper;
    };

    it("asks for the letter without taking the refusal from the interceptor", async () => {
      const post = vi
        .spyOn(Api, "post")
        .mockResolvedValue({ data: null, error: null } as never);

      await askForTheLetter();

      const call = post.mock.calls.at(-1);
      expect(call?.[0]).toBe("account/recovery");
      expect(call?.[1]).toEqual({ email: "reader@example.com" });
      const options = call?.[2] as Flags | undefined;
      expect(options?.ownsRefusal).toBeUndefined();
      expect(options?.ownsRateLimit).toBeUndefined();
    });

    /** A refusal the card can say stands over the button, once, with no plate. */
    it("names a refusal over the button itself, not in a plate", async () => {
      vi.spyOn(Api, "post").mockResolvedValue({
        data: null,
        error: { status: 409, title: "Аккаунт уже активирован" },
      } as never);

      const wrapper = await askForTheLetter();

      expect(wrapper.get(".activation-refusal").text()).toBe(
        "Аккаунт уже активирован",
      );
      expect(toasts.value.map((raised) => raised.message)).toEqual([]);
      // No receipt for a letter that never went out.
      expect(wrapper.text()).not.toContain("Проверьте почту");
    });
  });

  /** The reader holds a link, not a token. */
  it("calls the link invalid and names both causes", async () => {
    const wrapper = await arrive({ data: null, error: { status: 404 } });

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.get(".auth-status").classes()).toContain(
      "auth-status--error",
    );

    // The account is there already, or the registration is gone.
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    expect(wrapper.find(".auth-note").exists()).toBe(false);
    expect(wrapper.find(".auth-footnote").exists()).toBe(true);
  });

  /** One way out of a dead link, as on every screen that answers one. */
  it("gives one way out, the home page", async () => {
    const wrapper = await arrive({ data: null, error: { status: 404 } });
    const ways = wrapper.findAll("button");

    expect(ways.map((button) => button.text())).toEqual(["На главную"]);
    // The single action of the card, as wide as it.
    expect(ways[0].classes()).toContain("auth-button");
    expect(wrapper.find(".form-actions").exists()).toBe(false);

    await ways[0].trigger("click");
    expect(push).toHaveBeenLastCalledWith("/");
  });

  /** A link with nothing in it: no line of the card's own and no word of the database. */
  it("writes no line of its own for a link with nothing in it", async () => {
    window.location.hash = "";
    const check = vi.spyOn(accountApi, "getActivationInfo");

    const wrapper = mount(AccountActivationPage, options());
    await flushPromises();

    expect(check).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.text()).not.toContain("Токен");
    // The card explains itself in the reader's words.
    expect(wrapper.get(".auth-text").text()).toContain(
      "Возможно ссылка уже была использована",
    );
  });

  /** A name taken between the check and the press, refused in the words of the field. */
  it("refuses a taken name in the words of the field", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 400, errors: { username: ["Taken"] } },
    } as never);

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    // Back to the field, one sentence above the button.
    expect(wrapper.text()).toContain("Выберите имя");
    expect(wrapper.get(".activation-refusal").text()).toBe("Это имя занято");
    expect(wrapper.text()).not.toContain("Уже занято");
    expect(wrapper.text()).not.toContain("уже используется");
  });

  /** The server's sentence in the line under the heading, as on the removal pages. */
  it("puts the server sentence in the line under the heading", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 404, errors: { token: ["Ссылка уже использована"] } },
    } as never);

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe("Ссылка уже использована");
  });

  /** And says nothing at all when the server named nothing. */
  it("explains in its own words when the server sent none", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);

    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toContain(
      "Возможно ссылка уже была использована",
    );
  });

  /** The step back is a button that looks like the guest surface's secondary action. */
  it("goes back to the choice of the name from the button Изменить", async () => {
    const wrapper = await atConfirmStep();
    const back = wrapper.get(".auth-label-row button");

    expect(back.text()).toBe("Изменить");
    expect(back.attributes("disabled")).toBeUndefined();
    await back.trigger("click");
    expect(wrapper.text()).toContain("Выберите имя");
  });

  it("shuts Изменить while the activation is on the wire", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockImplementation(
      () => new Promise(() => {}) as never,
    );

    await wrapper.get(".auth-button").trigger("click");

    expect(wrapper.get(".auth-label-row button").attributes("disabled")).toBe(
      "",
    );
    expect(wrapper.text()).toContain("Подтвердите выбор имени");
  });

  /** The field's green verdict goes while the server's refusal of the name stands. */
  it("puts out the verdict of the field while a refusal stands", async () => {
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 400, errors: { username: ["Taken"] } },
    } as never);

    const wrapper = await arrive(ready);
    await chooseName(wrapper);
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.get(".activation-refusal").text()).toBe("Это имя занято");
    expect(wrapper.get(".activation-name__field").classes()).toContain(
      "activation-name__field--refused",
    );
  });

  /** The letter opened in two tabs: both end on the finished screen, the name out of the message. */
  describe("a second tab of the same link", () => {
    const { toasts, dismiss } = useToast();

    beforeEach(() => {
      TabChannel.last = null;
      toasts.value.forEach((raised) => dismiss(raised.id));
    });

    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it("shows the same finished screen with the name from the message", async () => {
      withTabs();
      const wrapper = await arrive(ready);

      await activatedElsewhere();

      expect(wrapper.get(".auth-title").text()).toBe("Регистрация завершена");
      expect(wrapper.get(".auth-text").text()).toBe("Добро пожаловать, reader");
      expect(wrapper.get(".auth-status").classes()).toContain(
        "auth-status--ok",
      );
      // Nothing has gone wrong, so no help is offered.
      expect(wrapper.find(".auth-footnote").exists()).toBe(false);

      // The news is on the card: nothing moves the reader or speaks over it.
      expect(push).not.toHaveBeenCalled();
      expect(toasts.value).toHaveLength(0);
    });

    /** Its control works from the name the message brought: this tab never held the account. */
    it("leads to the profile by the name from the message", async () => {
      withTabs();
      const wrapper = await arrive(ready);
      await activatedElsewhere("another");

      await wrapper.get("button.auth-button").trigger("click");

      expect(push).toHaveBeenLastCalledWith("/users/another");
    });

    /** The half the screen above stands on: the name goes into the message. */
    it("sends the name into the channel, not the event alone", async () => {
      withTabs();
      const wrapper = await atConfirmStep();
      vi.spyOn(accountApi, "activate").mockResolvedValue({
        data: { resource: { id: "1", username: "reader" } as User },
        error: null,
      } as Awaited<ReturnType<typeof accountApi.activate>>);

      await wrapper.get(".auth-button").trigger("click");
      await flushPromises();

      // The name of the channel is the other half of the contract.
      expect(openedChannel().name).toBe("dm_activation");
      expect(openedChannel().posted).toEqual([
        { type: "activated", username: "reader" },
      ]);
    });

    /** Both tabs pressed: the dead link the loser earns does not replace the finished screen. */
    it("keeps the finished screen over a refusal of its own attempt", async () => {
      withTabs();
      const wrapper = await atConfirmStep();

      let answer: (value: unknown) => void = () => {};
      vi.spyOn(accountApi, "activate").mockReturnValue(
        new Promise((resolve) => {
          answer = resolve;
        }) as never,
      );

      await wrapper.get(".auth-button").trigger("click");
      await activatedElsewhere();
      answer({ data: null, error: { status: 404 } });
      await flushPromises();

      expect(wrapper.get(".auth-title").text()).toBe("Регистрация завершена");
      expect(wrapper.text()).not.toContain("Ссылка недействительна");
    });

    /** The same for the check of the link, which runs on the way in. */
    it("keeps the finished screen over the answer of the link check", async () => {
      withTabs();
      let answer: (value: unknown) => void = () => {};
      vi.spyOn(accountApi, "getActivationInfo").mockReturnValue(
        new Promise((resolve) => {
          answer = resolve;
        }) as never,
      );

      const wrapper = mount(AccountActivationPage, options());
      await nextTick();
      await activatedElsewhere();
      answer({ data: null, error: { status: 404 } });
      await flushPromises();

      expect(wrapper.get(".auth-title").text()).toBe("Регистрация завершена");
    });

    /** A message with no name in it is not the finished screen. */
    it("shows no finished screen for a message without a name", async () => {
      withTabs();
      const wrapper = await arrive(ready);

      openedChannel().onmessage?.({ data: { type: "activated" } });
      await nextTick();

      expect(wrapper.text()).toContain("Выберите имя");
      expect(wrapper.text()).not.toContain("Регистрация завершена");
    });
  });
});
