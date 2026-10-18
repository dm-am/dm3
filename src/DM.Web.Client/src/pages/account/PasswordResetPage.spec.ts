/**
 * @vitest-environment jsdom
 */

/**
 * The page the password-recovery letter points at, on the one answer that asks
 * for something: the link checked out and there is a field to fill in.
 *
 * What is pinned here is the field and the line under it. The reader arrives
 * from a letter with one thing left to do, so the caret is already in the box;
 * the line under the box is the box's own description, so a reader who cannot
 * see the red bar is told what the red bar says; and the refusal the server
 * sent belongs to the password that earned it, so it goes the moment the
 * password is rewritten. Without that last rule the scale kept a verdict on a
 * password nobody could see any more, until "Сохранить" was pressed a second
 * time.
 *
 * The card while it is checking the link is pinned by tokenPages.spec.ts,
 * which covers this page together with the activation one. The two dead ends -
 * the link that ran out of time and the link that was never one - are pinned
 * here, because what stands on them is a way forward and a way forward is a
 * control.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import PasswordResetPage from "./PasswordResetPage.vue";

// One push for the whole file: every way out of this card is a router push, and
// a fresh vi.fn() per useRouter() call would leave nothing to read it from.
const { push } = vi.hoisted(() => ({ push: vi.fn() }));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push, replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/**
 * Attached to the document: a wrapper mounted off the tree has no active
 * element to be, and the caret is half of what this file is about.
 */
const options = () => ({
  global: {
    plugins: [createPinia()],
    stubs: { RouterLink: { template: "<a><slot /></a>" } },
  },
  attachTo: document.body,
});

const BREACHED = "Пароль встречается в утечках";

beforeEach(() => {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
  // Not a spy, so restoreAllMocks does not reach it.
  push.mockClear();
  document.body.innerHTML = "";
  // The value arrives in the fragment; readConfirmationToken takes it there.
  window.location.hash = "#token=a-token";
});

/** Land on the card with the form on it. */
const atForm = async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue({
    data: { status: "ready" },
    error: null,
  } as never);

  const wrapper = mount(PasswordResetPage, options());
  await flushPromises();
  return wrapper;
};

/** Land on the card with the answer the check came back with. */
const atAnswer = (answer: unknown) => async () => {
  vi.spyOn(accountApi, "getPasswordResetTokenInfo").mockResolvedValue(
    answer as never,
  );

  const wrapper = mount(PasswordResetPage, options());
  await flushPromises();
  return wrapper;
};

const atExpiredLink = atAnswer({ data: { status: "expired" }, error: null });
const atDeadLink = atAnswer({ data: null, error: { status: 404 } });

/** Hand in a password the server refuses. */
const handInRefusedPassword = async (wrapper: VueWrapper) => {
  vi.spyOn(accountApi, "completePasswordReset").mockResolvedValue({
    data: null,
    error: { status: 400, errors: { newPassword: [BREACHED] } },
  } as never);

  await wrapper.get("#reset-new-password").setValue("correct horse");
  await wrapper.get("form").trigger("submit");
  await flushPromises();
};

/** What the strip under the field says, and whether the bar is red. */
const line = (wrapper: VueWrapper) => wrapper.get(".auth-meter").text();
const barIsRed = (wrapper: VueWrapper) =>
  wrapper.get(".strength-fill").classes().includes("error");

describe("страница нового пароля", () => {
  it("ставит каретку в поле нового пароля", async () => {
    const wrapper = await atForm();

    expect(document.activeElement).toBe(
      wrapper.get("#reset-new-password").element,
    );

    wrapper.unmount();
  });

  /**
   * The heading one line above the field already says which password this is,
   * so the label says the noun and nothing else: written out in full, the card
   * made the reader read the same two words twice. A bare noun is also what
   * every other label of the guest contour is - "Почта", "Пароль".
   */
  it("не повторяет заголовок карточки в подписи поля", async () => {
    const wrapper = await atForm();

    expect(wrapper.get(".auth-title").text()).toBe("Новый пароль");
    expect(wrapper.get("label[for='reset-new-password']").text()).toBe(
      "Пароль",
    );

    wrapper.unmount();
  });

  /**
   * The refusal is about the password that earned it. The moment the field is
   * edited the server has not seen what is in it, so the verdict cannot stand:
   * it used to, red bar and all, until the button was pressed again.
   */
  it("гасит отказ сервера при первой же правке пароля", async () => {
    const wrapper = await atForm();
    await handInRefusedPassword(wrapper);

    expect(line(wrapper)).toContain(BREACHED);
    expect(barIsRed(wrapper)).toBe(true);

    await wrapper.get("#reset-new-password").setValue("correct horse battery");

    expect(line(wrapper)).not.toContain(BREACHED);
    expect(line(wrapper)).toBe("Надежный");
    expect(barIsRed(wrapper)).toBe(false);

    wrapper.unmount();
  });

  /**
   * The strip under the field is the field's own description: the rule while
   * nothing is typed, the strength once something is, the refusal once the
   * server has answered. Unnamed, a reader on a screen reader met a box with a
   * label and nothing else, and the red bar said nothing to him at all.
   */
  it("связывает строку под полем с самим полем", async () => {
    const wrapper = await atForm();
    const field = wrapper.get("#reset-new-password");

    expect(field.attributes("aria-describedby")).toBe("reset-password-message");
    expect(wrapper.get(".auth-meter").attributes("id")).toBe(
      "reset-password-message",
    );
    expect(field.attributes("aria-invalid")).toBeUndefined();

    wrapper.unmount();
  });

  /**
   * A refused password cannot be handed in a second time. The server turned
   * this exact one down - it is in a breach index - and the button stayed live
   * over the red line, so the only thing pressing it again could do was send
   * the same password and get the same answer. It opens on the first edit,
   * where the refusal is dropped. The registration form's second step has been
   * shut this way all along.
   */
  it("запирает кнопку, пока отказ сервера на экране", async () => {
    const wrapper = await atForm();
    const button = () => wrapper.get("button[type='submit']");

    await wrapper.get("#reset-new-password").setValue("correct horse");
    expect(button().attributes("disabled")).toBeUndefined();

    await handInRefusedPassword(wrapper);

    expect(line(wrapper)).toContain(BREACHED);
    expect(button().attributes("disabled")).toBe("");

    await wrapper.get("#reset-new-password").setValue("correct horse battery");

    expect(button().attributes("disabled")).toBeUndefined();

    wrapper.unmount();
  });

  /**
   * The card that ends the errand says the errand is done, the way the changed
   * address does one page over: a heading names the outcome and the line under
   * it says it happened.
   */
  it("говорит под заголовком, что пароль обновлен", async () => {
    const wrapper = await atForm();
    vi.spyOn(accountApi, "completePasswordReset").mockResolvedValue({
      data: {},
      error: null,
    } as never);

    await wrapper.get("#reset-new-password").setValue("correct horse battery");
    await wrapper.get("form").trigger("submit");
    await flushPromises();

    expect(wrapper.get(".auth-title").text()).toBe("Новый пароль сохранен");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Ваш пароль был успешно обновлен",
    );

    wrapper.unmount();
  });

  it("отмечает поле неверным, пока отказ сервера на экране", async () => {
    const wrapper = await atForm();
    await handInRefusedPassword(wrapper);

    expect(wrapper.get("#reset-new-password").attributes("aria-invalid")).toBe(
      "true",
    );

    await wrapper.get("#reset-new-password").setValue("correct horse battery");

    expect(
      wrapper.get("#reset-new-password").attributes("aria-invalid"),
    ).toBeUndefined();

    wrapper.unmount();
  });
});

/**
 * The two dead ends of the card, in the shape the activation page gives them.
 *
 * Both used to end in a sentence with a word underlined inside it: "Запросить
 * новую?" on one, "Попробуйте войти или запросить новую ссылку" on the other.
 * A way forward is a control, and the card next door had already been built
 * that way - so a reader who followed the same letter twice met the same dead
 * end drawn two different ways.
 */
describe("тупики страницы сброса пароля", () => {
  it("дает истекшей ссылке кнопку, которая называет дело", async () => {
    const wrapper = await atExpiredLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка устарела");
    // The heading one line above already says it is a link that ran out, so the
    // line under it names what ran out and not the link a second time.
    expect(wrapper.get(".auth-text").text()).toBe(
      "Срок действия сброса пароля истек",
    );

    // A control and not a word inside a sentence: the surface's single
    // full-width action, exactly as on the expired activation link.
    const ask = wrapper.get("button.auth-button");
    expect(ask.text()).toBe("Запросить новую ссылку");
    expect(wrapper.findAll(".auth-note a")).toHaveLength(0);

    await ask.trigger("click");
    expect(push).toHaveBeenLastCalledWith({
      path: "/",
      query: { action: "recovery" },
    });

    wrapper.unmount();
  });

  /**
   * One way out of a dead link, and the same one on all six screens that answer
   * one. The card used to stand the sign-in and a new letter side by side,
   * which asks the reader to name the cause of a failure he cannot see; both
   * doors stand on the home page anyway. Owner's decision.
   */
  it("дает мертвой ссылке один выход, и это главная", async () => {
    const wrapper = await atDeadLink();

    expect(wrapper.get(".auth-title").text()).toBe("Ссылка недействительна");
    expect(wrapper.get(".auth-text").text()).toBe(
      "Возможно ссылка уже была использована или срок ее действия истек",
    );

    const ways = wrapper.findAll("button");
    expect(ways.map((button) => button.text())).toEqual(["На главную"]);
    // The single action of the card, so it takes the whole width of it.
    expect(ways[0].classes()).toContain("auth-button");
    expect(wrapper.find(".form-actions").exists()).toBe(false);
    // And nothing is left underlined inside the sentence above it.
    expect(wrapper.findAll(".auth-note")).toHaveLength(0);

    await ways[0].trigger("click");
    expect(push).toHaveBeenLastCalledWith("/");

    wrapper.unmount();
  });
});
