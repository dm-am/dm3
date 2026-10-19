/**
 * @vitest-environment jsdom
 */

/**
 * The security block of the account page: both presses answer where the form
 * stood, the letter named by its address and the password change by the
 * sessions it ended. A receipt is built from the address sent, never on a
 * refusal, and no plate repeats it.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import AccountSecuritySection from "./AccountSecuritySection.vue";

/** The account refresh after a sent letter is stubbed: the block only awaits it. */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
  signOut: vi.fn(),
  signOutAll: vi.fn(),
}));

/** The account the block is drawn for. */
const reader = (email?: string) => ({ username: "reader", email }) as never;

function render(user = reader("old@dm.am")) {
  setActivePinia(createPinia());
  return mount(AccountSecuritySection, { props: { user } });
}

/** The two cards of the block, in the order the page draws them. */
const emailCard = (wrapper: VueWrapper) =>
  wrapper.findAll(".security-block")[0];
const passwordCard = (wrapper: VueWrapper) =>
  wrapper.findAll(".security-block")[1];

/** The button of a card that reads `label`. */
const button = (
  card: VueWrapper | ReturnType<VueWrapper["get"]>,
  label: string,
) => card.findAll("button").find((b) => b.text() === label)!;

/** Everything the site is saying in a floating message right now. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** Types an address and a password into the email card and presses it. */
async function askForNewAddress(wrapper: VueWrapper, address: string) {
  await wrapper.get("#new-email").setValue(address);
  await wrapper.get("#email-password").setValue("current-secret");
  await button(emailCard(wrapper), "Изменить почту").trigger("click");
  await flushPromises();
}

/** Fills the password card with a valid pair and presses it. */
async function changePassword(wrapper: VueWrapper) {
  await wrapper.get("#old-password").setValue("current-secret");
  await wrapper.get("#new-password").setValue("a-longer-secret");
  await wrapper.get("#confirm-password").setValue("a-longer-secret");
  await button(passwordCard(wrapper), "Изменить пароль").trigger("click");
  await flushPromises();
}

describe("the security block of the account page", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    fetchUser.mockResolvedValue(undefined);
    // Error toasts never dismiss themselves and deduplicate by text.
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
  });

  it("names the address the letter went to, and how long the answer says its link lives", async () => {
    // Not the default of the setting: the card says what the server answered.
    const sent = vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
      data: { resource: { linkLifetimeHours: 30 } },
      error: null,
    } as never);
    const wrapper = render();

    await askForNewAddress(wrapper, "new@dm.am");

    // The address of the receipt is the address of the request.
    expect(sent.mock.calls[0][0].email).toBe("new@dm.am");
    expect(emailCard(wrapper).get("strong").text()).toBe("new@dm.am");
    expect(emailCard(wrapper).text()).toContain(
      "Мы отправили письмо со ссылкой для смены почты на",
    );
    expect(emailCard(wrapper).text()).toContain(
      "Ссылка действительна 30 часов",
    );
    // The receipt stands where the form stood.
    expect(wrapper.find("#new-email").exists()).toBe(false);
    expect(plates()).toEqual([]);
  });

  // One line, as on the registration: a hint over the scale's line said the same thing twice.
  it("writes the shortest password the site takes in the one line of the scale", async () => {
    const wrapper = render();
    const minimum = () =>
      passwordCard(wrapper).text().split("Минимум 8 символов").length - 1;

    expect(passwordCard(wrapper).find(".form-field-hint").exists()).toBe(false);
    expect(minimum()).toBe(1);

    await wrapper.get("#new-password").setValue("short");

    expect(minimum()).toBe(1);
  });

  it("tells the page that the journal has an entry it has not read", async () => {
    vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = render();

    await askForNewAddress(wrapper, "new@dm.am");

    expect(wrapper.emitted("emailChangeRequested")).toHaveLength(1);
  });

  it("keeps the form and says nothing about a letter when the server refuses", async () => {
    vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
      data: null,
      error: { title: "Введенный пароль неверен" },
    } as never);
    const wrapper = render();

    await askForNewAddress(wrapper, "new@dm.am");

    expect(wrapper.find("#new-email").exists()).toBe(true);
    expect(emailCard(wrapper).text()).not.toContain("Мы отправили письмо");
    expect(emailCard(wrapper).text()).toContain("Введенный пароль неверен");
    expect(wrapper.emitted("emailChangeRequested")).toBeUndefined();
  });

  it("says the other sessions are over, where the password form stood", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = render();

    await changePassword(wrapper);

    expect(passwordCard(wrapper).text()).toContain(
      "Пароль изменен, остальные сессии завершены",
    );
    // The notice the server sends after the change, named by its address.
    expect(passwordCard(wrapper).text()).toContain(
      "Мы отправили письмо о смене пароля на",
    );
    expect(passwordCard(wrapper).get("strong").text()).toBe("old@dm.am");
    expect(wrapper.find("#old-password").exists()).toBe(false);
    expect(wrapper.emitted("passwordChanged")).toHaveLength(1);
    expect(plates()).toEqual([]);
  });

  it("promises no letter to an account that has no address", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = render(reader(undefined));

    await changePassword(wrapper);

    expect(passwordCard(wrapper).text()).toContain(
      "Пароль изменен, остальные сессии завершены",
    );
    expect(passwordCard(wrapper).text()).not.toContain("Мы отправили письмо");
  });

  it("keeps the password form and stays silent when the server refuses", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: { title: "Текущий пароль неверен" },
    } as never);
    const wrapper = render();

    await changePassword(wrapper);

    expect(wrapper.find("#old-password").exists()).toBe(true);
    expect(passwordCard(wrapper).text()).not.toContain("Пароль изменен");
    expect(passwordCard(wrapper).text()).toContain("Текущий пароль неверен");
    expect(wrapper.emitted("passwordChanged")).toBeUndefined();
  });

  /** A refusal is said in the line beside the button; the fields keep what was typed and no receipt is drawn. */
  it("says a 500 in the line of the email form", async () => {
    vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);
    const wrapper = render();

    await askForNewAddress(wrapper, "new@dm.am");

    expect(emailCard(wrapper).get(".action-result").text()).toBe(
      "Ошибка сервера",
    );
    expect(emailCard(wrapper).text()).not.toContain("Мы отправили письмо");
    // The address is still in the field.
    expect((wrapper.get("#new-email").element as HTMLInputElement).value).toBe(
      "new@dm.am",
    );
    expect(wrapper.emitted("emailChangeRequested")).toBeUndefined();
  });

  it("says a 500 in the line of the password form", async () => {
    vi.spyOn(accountApi, "changePassword").mockResolvedValue({
      data: null,
      error: { status: 500, title: "Ошибка сервера" },
    } as never);
    const wrapper = render();

    await changePassword(wrapper);

    expect(passwordCard(wrapper).get(".action-result").text()).toBe(
      "Ошибка сервера",
    );
    expect(passwordCard(wrapper).text()).not.toContain("Пароль изменен");
    expect(wrapper.emitted("passwordChanged")).toBeUndefined();
  });

  // An expired session and a dead connection are the plate's.
  it.each([0, 401])(
    "keeps both forms quiet about a %i, which the plate carries",
    async (status) => {
      vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
        data: null,
        error: { status, title: "" },
      } as never);
      vi.spyOn(accountApi, "changePassword").mockResolvedValue({
        data: null,
        error: { status, title: "" },
      } as never);
      const wrapper = render();

      await askForNewAddress(wrapper, "new@dm.am");
      await changePassword(wrapper);

      expect(emailCard(wrapper).find(".action-result").exists()).toBe(false);
      expect(passwordCard(wrapper).find(".action-result").exists()).toBe(false);
    },
  );
});
