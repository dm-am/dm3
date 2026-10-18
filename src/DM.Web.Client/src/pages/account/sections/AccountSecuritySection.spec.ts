/**
 * @vitest-environment jsdom
 */

/**
 * The security block of the account page, and the two presses that used to
 * answer in the corner of the screen.
 *
 * Both of them do something the block itself can show, so both now answer where
 * the form stood. The letter for a new address is named by the address it went
 * to, the way the six other screens that send a letter name theirs; the
 * password change says that the other sessions are over, because the server
 * ends them in the same call and the reader is looking here, not at the device
 * list four blocks down.
 *
 * The ways these receipts can lie are what the tests below press on. The
 * address is read out of a field that is emptied on the way to the receipt, so
 * a receipt built after the reset names nobody. A receipt drawn on a refusal
 * claims a letter that was never sent. And a plate raised beside a receipt says
 * the same thing twice, in the one place the reader is not looking.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import AccountSecuritySection from "./AccountSecuritySection.vue";

/**
 * The account refresh that follows a sent letter is a session call with a store
 * behind it, and this block only awaits it, so it is stubbed out.
 */
const fetchUser = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/lib/session", () => ({
  fetchUser,
  register: vi.fn(),
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

/** Everything the site is saying in a floating message right now. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** Types an address and a password into the email card and presses it. */
async function askForNewAddress(wrapper: VueWrapper, address: string) {
  await wrapper.get("#new-email").setValue(address);
  await wrapper.get("#email-password").setValue("current-secret");
  await emailCard(wrapper).get("button.save-button").trigger("click");
  await flushPromises();
}

/** Fills the password card with a valid pair and presses it. */
async function changePassword(wrapper: VueWrapper) {
  await wrapper.get("#old-password").setValue("current-secret");
  await wrapper.get("#new-password").setValue("a-longer-secret");
  await wrapper.get("#confirm-password").setValue("a-longer-secret");
  await passwordCard(wrapper).get("button.save-button").trigger("click");
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

  it("names the address the letter went to, and how long its link lives", async () => {
    const sent = vi
      .spyOn(accountApi, "changeEmail")
      .mockResolvedValue({ data: null, error: null } as never);
    const wrapper = render();

    await askForNewAddress(wrapper, "new@dm.am");

    // The address of the receipt is the address of the request: read after the
    // fields were emptied, the bold word here would be empty.
    expect(sent.mock.calls[0][0].email).toBe("new@dm.am");
    expect(emailCard(wrapper).get("strong").text()).toBe("new@dm.am");
    expect(emailCard(wrapper).text()).toContain(
      "Мы отправили письмо со ссылкой для смены почты на",
    );
    expect(emailCard(wrapper).text()).toContain("Ссылка действительна 24 часа");
    // The receipt stands where the form stood.
    expect(wrapper.find("#new-email").exists()).toBe(false);
    expect(plates()).toEqual([]);
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

  /**
   * The refusals the response interceptor used to plate - a 403, a 429, a 5xx
   * - and where they stand now: in the line beside the button of the block,
   * with the plate taken down (refusalHere; the owner's rule of 26 September
   * 2026). The reader looks at the form, and the corner is the one place they
   * do not. The fields keep what was typed into them and no receipt is drawn,
   * so the block still says the letter never went out.
   */
  const REFUSED: [number, string][] = [
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
  ];

  it.each(REFUSED)(
    "says a %i in the line of the email form",
    async (status, line) => {
      vi.spyOn(accountApi, "changeEmail").mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      } as never);
      const wrapper = render();

      await askForNewAddress(wrapper, "new@dm.am");

      expect(emailCard(wrapper).get(".error-text").text()).toBe(line);
      expect(emailCard(wrapper).text()).not.toContain("Мы отправили письмо");
      // The address is still in the field, so the press can be repeated.
      expect(
        (wrapper.get("#new-email").element as HTMLInputElement).value,
      ).toBe("new@dm.am");
      expect(wrapper.emitted("emailChangeRequested")).toBeUndefined();
    },
  );

  it.each(REFUSED)(
    "says a %i in the line of the password form",
    async (status, line) => {
      vi.spyOn(accountApi, "changePassword").mockResolvedValue({
        data: null,
        error: { status, title: "Ошибка сервера" },
      } as never);
      const wrapper = render();

      await changePassword(wrapper);

      expect(passwordCard(wrapper).get(".error-text").text()).toBe(line);
      expect(passwordCard(wrapper).text()).not.toContain("Пароль изменен");
      expect(wrapper.emitted("passwordChanged")).toBeUndefined();
    },
  );

  // An expired session and a dead connection are about the reader, not the
  // press: the plate says them, and the forms keep quiet.
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

      expect(emailCard(wrapper).find(".error-text").exists()).toBe(false);
      expect(passwordCard(wrapper).find(".error-text").exists()).toBe(false);
    },
  );
});
