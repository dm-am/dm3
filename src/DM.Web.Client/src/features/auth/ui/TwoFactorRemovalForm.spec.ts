/**
 * @vitest-environment jsdom
 */

/**
 * The mailed way back for somebody who lost both the phone and the codes.
 *
 * Two properties carry the whole screen. It answers the same for every address,
 * because the server does: a result screen that said "аккаунт не найден" would
 * turn this form into a way of asking which accounts carry a second factor. And
 * a failure is not a sent letter: reported as one, the reader waits for a
 * message nobody posted, which is the state this path exists to get them out
 * of.
 *
 * The guest surface adds two more, and both are one attribute away from being
 * lost. The card has no "Отмена" and no grey footer - the cross of AuthDialog
 * is the way out, and the one filled control names the result the press
 * produces. And the strip under the field is rendered whether it has anything
 * to say or not, because it is what holds the card's height still: put a v-if
 * on it and the button jumps a line up the moment a refusal arrives.
 */
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { Api } from "@/shared/api";
import TwoFactorRemovalForm from "./TwoFactorRemovalForm.vue";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));
vi.mock("vue-router", () => ({ useRouter: () => ({ push }) }));

// One instance, so "the card said it and nothing else did" can be asserted.
const toast = vi.hoisted(() => ({
  error: vi.fn(),
  warning: vi.fn(),
  success: vi.fn(),
}));

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => toast,
}));

enableAutoUnmount(afterEach);

/**
 * The dialog frame is a vue-final-modal wrapper; the card inside it is the
 * subject. Attached to the document, because focus is only observable there.
 */
const options = () => ({
  attachTo: document.body,
  global: {
    plugins: [createPinia()],
    stubs: {
      AuthDialog: { name: "AuthDialog", template: "<div><slot /></div>" },
    },
  },
});

const mountForm = () => mount(TwoFactorRemovalForm, options());

async function submit(email = "reader@dm.am") {
  const wrapper = mountForm();

  await wrapper.find("#two-factor-removal-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

describe("the mailed removal request", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  /**
   * The card is named by the errand and not by the trouble that brought the
   * reader to it: the heading used to repeat the question the link asks ("Нет
   * доступа к приложению?"). Owner's decision.
   *
   * The errand and not the term either: the term is "двухфакторная
   * аутентификация" (GLOSSARY, section 8), and it is half again as wide as the
   * heading of this card can be (guestTitleFits.spec.ts). So the heading names
   * the step, as the three screens the letter leads to do, and the term stands
   * in full in the box under it. What the card must not do is call the thing
   * "защита" in one line and "двухфакторная аутентификация" in the next, which
   * is what it did.
   */
  it("asks for the address", () => {
    const wrapper = mountForm();

    expect(wrapper.get(".auth-title").text()).toBe("Запрос на отключение");
    expect(wrapper.text()).not.toContain("Нет доступа к приложению?");
    expect(
      wrapper.text(),
      'у термина одно слово: "двухфакторная аутентификация", не "защита"',
    ).not.toContain("защит");
    expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
  });

  /**
   * The delay is the whole shape of this path, so it is said in the caution box
   * the confirmation screen asks in (TwoFactorRemovalPage) and not in two lines
   * of loose prose: same box, same headline, centred, and the fact in bold.
   *
   * The lines it replaces both misdescribed the letter. "Отключим защиту через
   * 7 дней" counted the week from the press, when it starts at the link; and
   * the sign-in that calls a scheduled removal off is the second letter's to
   * explain, not this card's, which has nothing scheduled yet.
   */
  it("puts the delay in a centred warning box", () => {
    const box = mountForm().get(".auth-warnbox");
    const lead = box.get("b").text();

    expect(box.classes()).toContain("auth-warnbox--notice");
    expect([lead, box.text().slice(lead.length)]).toEqual([
      "Отключение выполняется с задержкой",
      "Ссылка из письма назначит отключение двухфакторной аутентификации через 7 дней",
    ]);
  });

  it("drops the lines that counted the week from the press", () => {
    const wrapper = mountForm();

    expect(wrapper.text()).not.toContain("Отключим защиту через 7 дней");
    expect(wrapper.text()).not.toContain("Вход с кодом отменит отключение");
  });

  // The one filled control of the card names the result of pressing it. Beside
  // it stands the foot of the card, and nothing else: the way out with nothing
  // done is the cross of the dialog frame.
  it("offers one control, and it names the result", () => {
    const wrapper = mountForm();

    const captions = wrapper.findAll("button").map((b) => b.text());
    expect(captions).toEqual(["Отправить письмо", "поддержку"]);
  });

  // The reader of this card came here without the phone and without the codes,
  // so the address is all it can ask for. An address he cannot reach either
  // leaves him nowhere, and this is the only line on the card that leads to a
  // person. Not on the receipt above it: there the letter is already out.
  describe("no access to the mailbox either", () => {
    it("closes the dialog and opens the support form", async () => {
      const wrapper = mountForm();

      expect(wrapper.text()).toContain("Нужна помощь? Обратитесь в");
      await wrapper
        .findAll("button")
        .find((b) => b.text() === "поддержку")
        ?.trigger("click");
      await flushPromises();

      expect(wrapper.emitted("cancel")).toHaveLength(1);
      expect(push).toHaveBeenCalledWith("/support?subtype=AccessRecovery");
    });

    it("leaves the receipt without the offer", async () => {
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: null,
      });

      const wrapper = await submit();

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).not.toContain("Нужна помощь");
    });
  });

  // The card opens with one empty field and asks for one thing. A caret the
  // reader has to place himself is a click spent on nothing.
  it("puts the caret in the field it opens with", async () => {
    const wrapper = mountForm();
    await flushPromises();

    expect(document.activeElement).toBe(
      wrapper.find("#two-factor-removal-email").element,
    );
  });

  /**
   * The button used to stay grey until the address passed the format check, so
   * a reader who had mistyped it was left in front of a dead control with
   * nothing on the card to say why. The press is what asks the question now,
   * and the strip under the field carries the answer: an empty field still
   * shuts the button, because there is nothing to send and nothing to say.
   */
  it("opens the button on anything typed and names the format on the press", async () => {
    const send = vi
      .spyOn(accountApi, "requestTwoFactorRemoval")
      .mockResolvedValue({ data: null, error: null } as never);
    const wrapper = mountForm();
    const button = () => wrapper.find("button[type='submit']");

    expect(button().attributes("disabled")).toBeDefined();

    await wrapper.find("#two-factor-removal-email").setValue("reader@");
    expect(button().attributes("disabled")).toBeUndefined();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(send).not.toHaveBeenCalled();
    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Неверный формат почты");
    expect(strip.classes()).toContain("auth-message--error");
  });

  // The strip is the height under the field. Empty, it still occupies it, so
  // the card does not resize around the reader when a refusal arrives.
  it("keeps the strip under the field with nothing in it", () => {
    const wrapper = mountForm();

    const strip = wrapper.find(".auth-message");
    expect(strip.exists()).toBe(true);
    expect(strip.text()).toBe("");
    expect(strip.classes()).not.toContain("auth-message--error");
  });

  /**
   * A server that could not post the letter answers 500, and the interceptor
   * says so in a toast that fades after eight seconds over a card that has not
   * changed - so the refusal was lost and the reader was left waiting for a
   * letter nobody sent. It stands in the strip now, like every other refusal
   * of this form.
   */
  it("says under the field that the letter did not go out", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Ошибка сервера");
    expect(strip.classes()).toContain("auth-message--error");
  });

  /**
   * A request that never got an answer has no title to relay, and the card used
   * to invent one for it: "Не удалось отправить письмо. Попробуйте позже."
   * standing next to the server's own "Ошибка сервера" gave one failure two
   * names, and both of the client's own were two sentences with a full stop
   * apiece. One wording now, the one the API titles a 500 with.
   */
  it("says the same thing when nothing came back at all", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 0, title: "" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    expect(wrapper.find(".auth-message").text()).toBe("Ошибка сервера");
    expect(wrapper.text()).not.toContain("Попробуйте позже");
  });

  it("keeps the form up when the letter did not go out", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Неверный формат почты" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    const strip = wrapper.find(".auth-message");
    expect(strip.text()).toBe("Неверный формат почты");
    expect(strip.classes()).toContain("auth-message--error");
  });

  /**
   * Every refusal of this card is printed in its strip, and it is one sentence
   * and not two only because the press asked the response interceptor to keep
   * out of this one. The press, and not the service it goes through: the
   * service forwards what it is handed and claims nothing of its own
   * (accountApi.spec.ts), because a flag fixed inside it would decide for every
   * screen that sends the same request, including the ones that show nothing
   * and need their plate. The client stays quiet for a request that carries the
   * flags (client.spec.ts). Neither of those two knows which call this form
   * presses, and the tests around this one are blind to it as well - they
   * answer in place of the service, so the flags never reach a client at all.
   *
   * So the press is followed through the real service down to the client here.
   * Sent without them, a 500 puts "Ошибка сервера" in the strip and the same
   * sentence in a plate above it, and a 429 leaves a toast fading over a button
   * that is still counting down - in front of a reader who has already run out
   * of other ways in.
   */
  it("presses with the refusal and the limit taken over from the interceptor", async () => {
    const post = vi
      .spyOn(Api, "post")
      .mockResolvedValue({ data: null, error: null } as never);

    await submit("nobody@dm.am");

    expect(post).toHaveBeenCalledWith(
      "account/two-factor/removal",
      { email: "nobody@dm.am" },
      expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
    );
  });

  /**
   * This path is anonymous and sits on the address budget, so the rate limit is
   * the refusal it actually meets — and it meets it from a reader who has
   * already run out of other ways in.
   *
   * As a toast it faded after eight seconds over a card that had not changed —
   * button live, strip empty — so he pressed again, bought the same refusal,
   * and concluded the form was broken. It now stands in the strip the card
   * already reserves, with the wait running down and the button shut.
   */
  describe("a rate limit", () => {
    const limited = (retryAfterSeconds?: number) =>
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: {
          status: 429,
          title: "Слишком много запросов",
          retryAfterSeconds,
        },
      } as never);

    beforeEach(() => {
      // Only the interval: everything else in this file runs on real time.
      vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it("stands under the field, shuts the button, and runs down", async () => {
      limited(3);
      const wrapper = await submit();

      expect(wrapper.text()).not.toContain("Проверьте почту");
      expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
      const strip = wrapper.find(".auth-message");
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 3 секунды",
      );
      expect(strip.classes()).toContain("auth-message--error");
      expect(wrapper.find("button.auth-button").attributes("disabled")).toBe(
        "",
      );
      // One refusal, one carrier: the request took the limit over from the
      // interceptor precisely so this stays empty.
      expect(toast.warning).not.toHaveBeenCalled();

      vi.advanceTimersByTime(1000);
      await nextTick();
      expect(strip.text()).toBe(
        "Слишком много попыток, попробуйте через 2 секунды",
      );

      vi.advanceTimersByTime(2000);
      await nextTick();
      expect(strip.text()).toBe("");
      expect(
        wrapper.find("button.auth-button").attributes("disabled"),
      ).toBeUndefined();
    });

    // The modal keeps this component mounted when it closes, so an interval
    // left running ticks on behind a screen nobody is looking at.
    it("drops the wait when the dialog closes", async () => {
      limited(30);
      const wrapper = await submit();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.findComponent({ name: "AuthDialog" }).vm.$emit("beforeClose");
      await nextTick();

      expect(vi.getTimerCount()).toBe(0);
      expect(wrapper.find(".auth-message").text()).toBe("");
    });

    it("drops the wait when the card leaves the page", async () => {
      limited(30);
      const wrapper = await submit();
      expect(vi.getTimerCount()).toBe(1);

      wrapper.unmount();

      expect(vi.getTimerCount()).toBe(0);
    });
  });

  /**
   * One screen for every address: whether an account exists, and whether it
   * carries a factor, is not this endpoint's to disclose.
   *
   * It used to keep that secret by announcing it - "Если аккаунт есть, письмо
   * уже в пути" tells the reader there is a question here, and then refuses to
   * answer it. The same sentence for every address keeps exactly as much back
   * and asks nothing of the reader: what went out, and where it went.
   */
  describe("the receipt", () => {
    beforeEach(() => {
      vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
        data: null,
        error: null,
      } as never);
    });

    it("names the letter and the address, and discloses nothing else", async () => {
      const wrapper = await submit("nobody@dm.am");

      expect(wrapper.text()).toContain("Проверьте почту");
      expect(wrapper.text()).toContain(
        "Мы отправили письмо со ссылкой для отключения двухфакторной аутентификации на",
      );
      expect(wrapper.text()).toContain("nobody@dm.am");
      expect(
        wrapper.text(),
        'у термина одно слово: "двухфакторная аутентификация", не "защита"',
      ).not.toContain("защит");
      expect(wrapper.text()).not.toContain("Если аккаунт есть");
      expect(wrapper.text()).not.toContain("не найден");
      expect(wrapper.find("#two-factor-removal-email").exists()).toBe(false);
    });

    /**
     * The letter goes out on the press and the link in it is good for a day;
     * the week starts when that link is followed. The grey line used to read
     * "Ссылка отключит защиту через 7 дней", which left the reader unable to
     * tell whether the letter itself was a week away.
     */
    it("names the life of the link and not the waiting period", async () => {
      const wrapper = await submit();

      expect(wrapper.text()).toContain("Ссылка действительна 24 часа");
      expect(wrapper.text()).not.toContain("7 дней");
    });

    // Nothing is left to do here, and the cross of the dialog frame is the way
    // out: a "Закрыть" button is a second way out that has to be aimed at.
    it("carries no button", async () => {
      const wrapper = await submit();

      expect(wrapper.findAll("button")).toHaveLength(0);
    });
  });
});
