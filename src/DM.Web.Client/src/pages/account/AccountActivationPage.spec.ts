/**
 * @vitest-environment jsdom
 */

/**
 * The page the activation letter leads to, in the shape the approved guest
 * prototype gave it: one card of one width for all five answers, standing on
 * the guest surface with a frame of its own because nothing dims the page
 * behind it.
 *
 * What is pinned here is what a reader sees and what he can still press —
 * which card the answer stands on, which answer offers help and which does
 * not, that the resend has exactly one attempt, that the caret is already in
 * the one field of the screen, and that the card never invents a sentence the
 * server did not send. The "checking the link" line is pinned by
 * tokenPages.spec.ts, which covers this page together with its sibling.
 */
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { Api } from "@/shared/api";
import { accountApi, UsernameInput } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import AccountActivationPage from "./AccountActivationPage.vue";
// The page as it is written: the rule below lives in the stylesheet and never
// reaches the rendered tree. Vite hands the file over as text.
import SOURCE from "./AccountActivationPage.vue?raw";

// One push for the whole file. The ways out of this card are router pushes and
// nothing else, so a fresh vi.fn() handed out on every useRouter() call would
// leave the test with no way to read where a button led.
const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

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

/**
 * Land on the page with the answer the check came back with.
 *
 * `attach` puts the card in the document, which is what the caret needs: a
 * wrapper mounted off the tree has no active element to be.
 */
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

/**
 * The channel the two tabs of one letter talk over, in a shape the test can
 * speak into: it hands over the instance the page opened and keeps what the
 * page posted into it.
 */
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
  if (!TabChannel.last) throw new Error("страница не открыла канал вкладок");
  return TabChannel.last;
};

/** What the other tab says when it has finished the activation. */
const activatedElsewhere = (username = "reader") => {
  openedChannel().onmessage?.({ data: { type: "activated", username } });
  return nextTick();
};

/** Choose a free name and step to the confirmation. */
const atConfirmStep = async () => {
  const wrapper = await arrive(ready);
  const name = wrapper.findComponent(UsernameInput);

  name.vm.$emit("update:modelValue", "reader");
  name.vm.$emit("availability", true);
  await nextTick();
  await wrapper.get(".submit-button").trigger("click");

  return wrapper;
};

describe("страница активации", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    sessionStorage.clear();
    localStorage.clear();
    vi.restoreAllMocks();
    // Not a spy, so restoreAllMocks does not reach it.
    push.mockClear();
    // Cards left in the document by an attached mount would answer the next
    // test's selectors.
    document.body.innerHTML = "";
  });

  /**
   * The card used to be an element with a shadow, and the invalid-link answer
   * was 100px wider than the other four: three states of one flow arriving as
   * three differently shaped pages.
   */
  it.each([
    ["выбор имени", ready],
    ["устаревшую ссылку", expired],
  ])("ставит %s на одну гостевую карточку с рамкой", async (_, info) => {
    const wrapper = await arrive(info);

    expect(
      wrapper
        .find(".auth-surface > .auth-card--framed.auth-card--center")
        .exists(),
    ).toBe(true);
    expect(wrapper.find(".activation-page--wide").exists()).toBe(false);
  });

  it("держит поле имени на гостевом поле и на карточке той же ширины", async () => {
    const wrapper = await arrive(ready);

    expect(wrapper.text()).toContain("Выберите имя");
    expect(wrapper.get("label[for='activation-username']").text()).toBe(
      "Имя пользователя",
    );
    expect(wrapper.findComponent(UsernameInput).classes()).toContain(
      "auth-field",
    );
  });

  /**
   * The screen holds one field and the reader came here to fill it in. He
   * arrives from a letter, on a page whose check answers a render later, so
   * the caret was left in the document and every reader had to click into the
   * field before the first keystroke landed anywhere.
   */
  it("ставит каретку в поле имени, как только оно на экране", async () => {
    const wrapper = await arrive(ready, true);

    expect(document.activeElement).toBe(
      wrapper.get("#activation-username").element,
    );

    wrapper.unmount();
  });

  it("держит Продолжить отключенной, пока имя не свободно", async () => {
    const wrapper = await arrive(ready);
    expect(wrapper.get(".submit-button").attributes("disabled")).toBe("");

    const name = wrapper.findComponent(UsernameInput);
    name.vm.$emit("update:modelValue", "reader");
    name.vm.$emit("availability", true);
    await nextTick();

    expect(
      wrapper.get(".submit-button").attributes("disabled"),
    ).toBeUndefined();
  });

  /**
   * The name is fixed for good, so the step that fixes it says so in the red
   * frame the guest surface keeps for a consequence, and shows the name in the
   * plate that cannot be typed into.
   */
  it("предупреждает о необратимости имени и показывает его плашкой", async () => {
    const wrapper = await atConfirmStep();

    expect(wrapper.text()).toContain("Подтвердите выбор имени");
    const warning = wrapper.get(".auth-warnbox");
    expect(warning.text()).toContain("Будьте внимательны");
    expect(warning.text()).toContain(
      "Дальнейшая смена имени возможна лишь в исключительных случаях",
    );
    expect(wrapper.get(".auth-value").text()).toBe("reader");
    expect(wrapper.get(".submit-button").text()).toBe("Завершить регистрацию");
  });

  /**
   * Nothing has gone wrong on the finished screen, so there is nobody to ask
   * for help: the offer stands under the other four answers and not this one.
   *
   * And the greeting carries no exclamation mark. The heading above it already
   * says the whole news, so the bang congratulated the reader a second time.
   */
  // The account starts on the clock of the device it is finished on: the site
  // shows a signed-in reader's time in the zone of the account.
  it("передает часовой пояс устройства вместе с именем", async () => {
    const wrapper = await atConfirmStep();
    const activate = vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: { resource: { id: "1", username: "reader" } as User },
      error: null,
    } as Awaited<ReturnType<typeof accountApi.activate>>);

    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    expect(activate).toHaveBeenCalledWith(
      expect.any(String),
      expect.objectContaining({
        timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
      }),
    );
  });

  it("оставляет завершенную регистрацию без строки о поддержке", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: { resource: { id: "1", username: "reader" } as User },
      error: null,
    } as Awaited<ReturnType<typeof accountApi.activate>>);

    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Регистрация завершена");
    expect(wrapper.get(".auth-text").text()).toBe("Добро пожаловать, reader");
    expect(wrapper.get("button.auth-button").text()).toBe("Перейти в профиль");
    expect(wrapper.get(".auth-status").classes()).toContain("auth-status--ok");
    expect(wrapper.find(".help-section").exists()).toBe(false);
  });

  /**
   * One attempt from this screen: the offer is REPLACED by the receipt rather
   * than joined by it, so there is no button left to press a second time.
   *
   * And the receipt is the one the registration ends on, word for word: the
   * reader who asked for a second letter is waiting for exactly what the first
   * one promised. It used to keep the warning mark and the heading of the dead
   * link above a line of its own, so a card that had just done what was asked
   * of it still carried the sign that something was wrong, and offered help
   * underneath.
   */
  it("подменяет предложение отправки распиской о письме", async () => {
    const wrapper = await arrive(expired);

    // The heading the expired reset link and the expired name change carry:
    // one event, one name for it. It used to name the letter as well.
    expect(wrapper.get(".auth-title").text()).toBe("Ссылка устарела");
    expect(wrapper.text()).toContain(
      "Отправить новую ссылку для активации на reader@example.com?",
    );
    expect(wrapper.text()).toContain("Ссылка действительна 48 часов");
    expect(wrapper.get(".auth-status").classes()).toContain(
      "auth-status--warn",
    );

    vi.spyOn(accountApi, "recover").mockResolvedValue({
      data: {},
      error: null,
    } as Awaited<ReturnType<typeof accountApi.recover>>);
    await wrapper.get(".auth-button").trigger("click");
    await flushPromises();

    expect(wrapper.get(".auth-title").text()).toBe("Проверьте почту");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Мы отправили повторное письмо со ссылкой для активации на reader@example.com",
    );
    expect(wrapper.get(".auth-note").text()).toBe(
      "Ссылка действительна 48 часов",
    );
    expect(wrapper.text()).not.toContain("устарела");
    expect(wrapper.find(".auth-status").exists()).toBe(false);
    expect(wrapper.find(".auth-button").exists()).toBe(false);
    expect(wrapper.find(".help-section").exists()).toBe(false);
  });

  /**
   * Who speaks when the second letter does not go out.
   *
   * This card has no strip for a failed press: the offer is replaced by the
   * receipt and nothing else on it changes. So the refusal belongs to the
   * response interceptor, and `notifyFailure` covers only the half the
   * interceptor stays out of - a 404, a 409, a rejected address.
   *
   * The two halves used to leave a hole between them. The same endpoint is
   * pressed by three guest forms that answer for themselves, and the flags
   * that keep the interceptor quiet for them were fixed inside the method, so
   * they travelled on this press too: the interceptor said nothing because of
   * the flags, `notifyFailure` said nothing because those statuses are the
   * interceptor's, and a 403, a 429 or a 5xx here reached the reader as a
   * button that had simply stopped working.
   *
   * The press is followed through the real service down to the client, because
   * that is where the flags would be: a test that answers in place of the
   * service never sees them. That a flagless request is one the interceptor
   * speaks for is held in client.spec.ts; that the method itself claims
   * nothing, in accountApi.spec.ts.
   */
  describe("повторное письмо", () => {
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

    it("просит письмо, не забирая отказ у перехватчика", async () => {
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

    /**
     * The other half, in the card's own voice: the refusal is named on the
     * card, over the button, in the sentence the server sent, and once - not
     * in a plate in the corner (the owner's rule of 26 September 2026). A
     * refusal the interceptor speaks for is named there too, and the plate it
     * held is taken down.
     */
    it("сама называет отказ над кнопкой, а не плашкой", async () => {
      vi.spyOn(Api, "post").mockResolvedValue({
        data: null,
        error: { status: 409, title: "Аккаунт уже активирован" },
      } as never);

      const wrapper = await askForTheLetter();

      expect(wrapper.get(".activation-refusal").text()).toBe(
        "Аккаунт уже активирован",
      );
      expect(toasts.value.map((raised) => raised.message)).toEqual([]);
      // And no receipt over a letter that never went out.
      expect(wrapper.text()).not.toContain("Проверьте почту");
    });
  });

  /**
   * The reader is holding a link, not a token: the old headings named the
   * thing in the database.
   */
  it("называет недействительной ссылку, а не токен", async () => {
    const wrapper = await arrive({ data: null, error: { status: 404 } });

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.text()).not.toContain("Токен регистрации");
    expect(wrapper.get(".auth-status").classes()).toContain(
      "auth-status--error",
    );

    // Two causes in one line: the account behind the link is already there, or
    // the pending registration it belonged to is gone.
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );
    expect(wrapper.find(".auth-note").exists()).toBe(false);
    expect(wrapper.find(".help-section").exists()).toBe(true);
  });

  /**
   * One way out, and the one every screen that answers a dead link gives. The
   * card used to stand the sign-in and the registration side by side, which
   * asks the reader to name the cause of a failure he cannot see; both doors
   * stand on the home page anyway. Owner's decision.
   */
  it("дает один выход, и это главная", async () => {
    const wrapper = await arrive({ data: null, error: { status: 404 } });
    const ways = wrapper.findAll("button");

    expect(ways.map((button) => button.text())).toEqual(["На главную"]);
    // The single action of the card, so it takes the whole width of it.
    expect(ways[0].classes()).toContain("auth-button");
    expect(wrapper.find(".form-actions").exists()).toBe(false);

    await ways[0].trigger("click");
    expect(push).toHaveBeenLastCalledWith("/");
  });

  /**
   * A link with nothing in it. The card used to write a line of its own between
   * the heading and the box of causes, and the only word it had for what was
   * missing was the name of the thing in the database — the jargon the owner
   * struck off these screens by name.
   */
  it("не пишет своей строки, когда в ссылке ничего нет", async () => {
    window.location.hash = "";
    const check = vi.spyOn(accountApi, "getActivationInfo");

    const wrapper = mount(AccountActivationPage, options());
    await flushPromises();

    expect(check).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.text()).not.toContain("Токен активации отсутствует");
    expect(wrapper.text()).not.toContain("Токен");
    // The card explains itself whether the server said anything or not, and
    // says it in the reader's words rather than in the server's.
    expect(wrapper.get(".auth-text").text()).toContain(
      "Возможно ссылка уже была использована",
    );
  });

  /**
   * A name taken between the check and the press. The field said one thing
   * about an unavailable name and the shared vocabulary said another - the
   * server answers in codes, and "Taken" reads as "Уже занято" everywhere else
   * on the site - so the reader who ran into the race was told in a third
   * wording what the field had already told him in the first.
   */
  it("отказывает в занятом имени той же фразой, что и поле", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 400, errors: { username: ["Taken"] } },
    } as never);

    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    // Back to the field, with one sentence about the name above the button.
    expect(wrapper.text()).toContain("Выберите имя");
    expect(wrapper.get(".activation-refusal").text()).toBe("Это имя занято");
    expect(wrapper.text()).not.toContain("Уже занято");
    expect(wrapper.text()).not.toContain("уже используется");
  });

  /**
   * The refusal the server sent stands where the two removal pages stand
   * theirs: one line under the heading, above the box of causes.
   */
  it("ставит фразу сервера строкой под заголовком", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 404, errors: { token: ["Ссылка уже использована"] } },
    } as never);

    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe("Ссылка уже использована");
  });

  /** And says nothing at all when the server named nothing. */
  it("объясняет своими словами, когда сервер не прислал фразы", async () => {
    const wrapper = await atConfirmStep();
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 404 },
    } as never);

    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toContain(
      "Возможно ссылка уже была использована",
    );
  });

  /**
   * The step back to the name is a button by semantics and a link by sight,
   * and the sight is the guest surface's own secondary action — the same one
   * the login dialog wears beside its password. A recipe of its own on this
   * page is how two controls of one idiom drift apart.
   */
  it("берет вторичное действие с гостевой поверхности", async () => {
    const wrapper = await atConfirmStep();
    const back = wrapper.get(".auth-label-row button");

    expect(back.text()).toBe("Изменить");
    expect(back.classes()).toContain("auth-action");
    expect(back.classes()).not.toContain("change-username");
  });

  it("не держит собственного рецепта кнопки-под-ссылку", () => {
    expect(SOURCE).not.toContain("inline-link-button");
    expect(SOURCE).not.toContain("change-username");
  });

  /**
   * The green verdict cannot stand over a name the server has just refused.
   *
   * The field checks the name a moment before the press and knows nothing of
   * what the submit came back with, so it kept printing "Имя свободно" and a
   * tick under the refusal. One screen, one answer.
   */
  it("puts out the verdict of the field while a refusal stands", async () => {
    vi.spyOn(accountApi, "activate").mockResolvedValue({
      data: null,
      error: { status: 400, errors: { username: ["Taken"] } },
    } as never);

    const wrapper = await arrive(ready);
    const name = wrapper.findComponent(UsernameInput);
    name.vm.$emit("update:modelValue", "reader");
    name.vm.$emit("availability", true);
    await nextTick();
    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();
    await wrapper.get(".submit-button").trigger("click");
    await flushPromises();

    expect(wrapper.get(".activation-refusal").text()).toBe("Это имя занято");
    expect(wrapper.get(".activation-name__field").classes()).toContain(
      "activation-name__field--refused",
    );
  });

  /**
   * The letter opened in two tabs. One of them finishes the activation and
   * says so over the channel; the other used to be carried off to the home
   * page and told about it in a toast - a card with a finished screen of its
   * own, answering the best news of the flow by leaving the screen.
   *
   * Both tabs end where the activation ends now: the same card, the same
   * words, the name out of the message.
   */
  describe("вторая вкладка той же ссылки", () => {
    const { toasts, dismiss } = useToast();

    beforeEach(() => {
      TabChannel.last = null;
      toasts.value.forEach((raised) => dismiss(raised.id));
    });

    afterEach(() => {
      vi.unstubAllGlobals();
    });

    it("показывает тот же экран успеха с именем из события", async () => {
      withTabs();
      const wrapper = await arrive(ready);

      await activatedElsewhere();

      expect(wrapper.get(".auth-title").text()).toBe("Регистрация завершена");
      expect(wrapper.get(".auth-text").text()).toBe("Добро пожаловать, reader");
      expect(wrapper.get(".auth-status").classes()).toContain(
        "auth-status--ok",
      );
      // Nothing has gone wrong here either, so there is nobody to ask for help.
      expect(wrapper.find(".help-section").exists()).toBe(false);

      // The news stands on the card, so the reader is not moved off it and
      // nothing is said over it.
      expect(push).not.toHaveBeenCalled();
      expect(toasts.value).toHaveLength(0);
    });

    /**
     * And the one control of that screen works from the name the message
     * brought: this tab never held the account, so a button reading anything
     * else would drop the reader on the home page.
     */
    it("ведет в профиль по имени из события", async () => {
      withTabs();
      const wrapper = await arrive(ready);
      await activatedElsewhere("another");

      await wrapper.get("button.auth-button").trigger("click");

      expect(push).toHaveBeenLastCalledWith("/users/another");
    });

    /** The half the screen above stands on: the name goes into the message. */
    it("уносит в канал имя, а не одно событие", async () => {
      withTabs();
      const wrapper = await atConfirmStep();
      vi.spyOn(accountApi, "activate").mockResolvedValue({
        data: { resource: { id: "1", username: "reader" } as User },
        error: null,
      } as Awaited<ReturnType<typeof accountApi.activate>>);

      await wrapper.get(".submit-button").trigger("click");
      await flushPromises();

      // The name of the channel is the other half of the contract: two tabs
      // listening on different names never hear each other.
      expect(openedChannel().name).toBe("dm_activation");
      expect(openedChannel().posted).toEqual([
        { type: "activated", username: "reader" },
      ]);
    });

    /**
     * Both tabs pressed. The loser's own attempt reaches a token that is spent
     * by the time it lands, and the dead link it earns must not take the
     * finished screen off the card it has just been put on.
     */
    it("не сменяет успех отказом, догнавшим его с этой вкладки", async () => {
      withTabs();
      const wrapper = await atConfirmStep();

      let answer: (value: unknown) => void = () => {};
      vi.spyOn(accountApi, "activate").mockReturnValue(
        new Promise((resolve) => {
          answer = resolve;
        }) as never,
      );

      await wrapper.get(".submit-button").trigger("click");
      await activatedElsewhere();
      answer({ data: null, error: { status: 404 } });
      await flushPromises();

      expect(wrapper.get(".auth-title").text()).toBe("Регистрация завершена");
      expect(wrapper.text()).not.toContain("Ссылка недействительна");
    });

    /** The same for the check of the link, which runs on the way in. */
    it("не затирает успех ответом проверки ссылки", async () => {
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
    it("не показывает успеха без имени в событии", async () => {
      withTabs();
      const wrapper = await arrive(ready);

      openedChannel().onmessage?.({ data: { type: "activated" } });
      await nextTick();

      expect(wrapper.text()).toContain("Выберите имя");
      expect(wrapper.text()).not.toContain("Регистрация завершена");
    });
  });
});
