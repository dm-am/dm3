/**
 * @vitest-environment jsdom
 */

/**
 * Support and complaint are two pages over one form, drawn after the error
 * report form of the old site: the reason, who writes, what happened, one
 * button.
 *
 * Both routes keep their own framing text and mount the same form; the list
 * of reasons and a few words differ. The contact keeps the old site's caption:
 * a guest leaves an address there, a signed-in author finds his own name in
 * the same field, which he cannot change. There is no subject line. The text
 * is written in the site's markup editor, and the refusal of a press stands in
 * the form, over the button.
 *
 * The reason comes before the form (the owner, 28.09: "как сделать так, чтобы
 * модерацию не заебывали хуйней"): the picker opens on "Выберите причину" and
 * the fields stand only under a chosen reason, with a note that says when to
 * write here and what can be done without the administration. A complaint
 * about a person and the removal of an account are filed from an account, as
 * on DM2, and the complaint names the violator and the link, both required.
 *
 * The mounts are of the pages, not of the form alone: what has to hold is the
 * composition, and a form checked on its own would keep passing after a page
 * grew a second copy of it.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia } from "pinia";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { LoginPrompt } from "@/features/auth";
import SupportPage from "./SupportPage.vue";
import ComplaintPage from "./ComplaintPage.vue";
import { SupportTicketForm } from "@/features/support-ticket";
import { TICKET_TEXT_MAX_LENGTH } from "@/entities/ticket";

// Every mount holds a tiptap editor; left mounted, its timers fire after the
// environment is torn down and fail the run.
enableAutoUnmount(afterEach);

// A function rather than an arrow: the markup editor watches its mode tabs with
// one, built with `new`, and vitest will not construct an arrow.
vi.stubGlobal(
  "ResizeObserver",
  vi.fn(function () {
    return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
  }),
);

// The form reads the address for the reason and resolves the guest's ticket
// page; the sign-in line reads it to keep the query. Nothing else on either
// page touches the router.
const { route } = vi.hoisted(() => ({
  route: { query: {} as Record<string, string> },
}));
vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({
    push: vi.fn(),
    resolve: (to: { params: { token: string } }) => ({
      href: `/support/track/${to.params.token}`,
    }),
  }),
}));

// A picker's DOM value can be written from the outside; the submitted payload
// cannot, so that is where what the form sends is checked.
const { createTicket } = vi.hoisted(() => ({ createTicket: vi.fn() }));
vi.mock("@/entities/ticket", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/ticket")>()),
  ticketApi: { createTicket },
}));

/** The form shell is registered by the app, not by the test runner. */
const options = () => ({
  global: {
    plugins: [createPinia()],
    components: { Form, FormField },
    stubs: { RouterLink: RouterLinkStub, Teleport: true },
  },
});

/** A signed-in viewer, as the auth store reads him back on creation. */
const signIn = (username = "Reader") =>
  localStorage.setItem(
    "user",
    JSON.stringify({ username, role: "RegularUser", settings: {} }),
  );

type Page = typeof SupportPage;

/** A page opened on a reason, as a link that knows it opens it. */
const openOn = (Page: Page, subtype?: string) => {
  route.query = subtype ? { subtype } : {};
  return mount(Page, options());
};

/** The text, as the editor hands it to the form. */
const write = async (wrapper: VueWrapper, text: string) => {
  wrapper.findComponent(BBCodeEditor).vm.$emit("update:modelValue", text);
  await flushPromises();
};

/** Fill in what a guest has to and press the button. */
const fillAndSend = async (wrapper: VueWrapper, text = "Что случилось") => {
  if (wrapper.find("#contact").exists()) {
    await wrapper.find("#contact").setValue("reader@example.com");
  }
  await write(wrapper, text);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
};

/** The reason the picker currently shows; empty when none is chosen. */
const chosenReason = (wrapper: VueWrapper) =>
  (wrapper.find("select").element as HTMLSelectElement).value;

/** Values of the reason picker, in the order the page offers them. */
const reasonsOn = (wrapper: VueWrapper) =>
  wrapper
    .find("select")
    .findAll("option")
    .map((option) => (option.element as HTMLOptionElement).value)
    .filter((value) => value !== "");

/** Captions of the fields, in the order the form asks. */
const labelsOn = (wrapper: VueWrapper) =>
  wrapper.findAll(".form-field-label label").map((label) => label.text());

/** Where the links of a mounted piece lead. */
const linksOn = (wrapper: VueWrapper) =>
  wrapper
    .findAllComponents(RouterLinkStub)
    .map((link) => ({ to: link.props("to"), text: link.text() }));

/** Where the links of the note under the reason lead. */
const noteLinksOn = (wrapper: VueWrapper) =>
  wrapper
    .findAllComponents(RouterLinkStub)
    .filter((link) => link.element.closest(".reason-note"))
    .map((link) => link.props("to"));

/** The lines of the note under the reason, as they read. */
const noteOn = (wrapper: VueWrapper) =>
  wrapper
    .findAll(".reason-note-line")
    .map((line) => line.text().replace(/\s+/g, " ").trim());

/** The hint of the field whose control has this id. */
const hintOf = (wrapper: VueWrapper, id: string) =>
  wrapper
    .find(`#${id}`)
    .element.closest(".form-field")
    ?.querySelector(".form-field-hint")
    ?.textContent?.trim();

describe("support and complaint", () => {
  beforeEach(() => {
    // No stored viewer: the guest wording is what both pages open with.
    localStorage.clear();
    // A bare address, as a reader who typed /support by hand arrives with.
    route.query = {};
    createTicket.mockReset();
    createTicket.mockResolvedValue({
      data: { trackingToken: "T-1" },
      error: null,
    });
  });

  it("mounts one and the same form on both pages", () => {
    expect(
      mount(SupportPage, options()).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
    expect(
      mount(ComplaintPage, options()).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
  });

  it("offers a different list of reasons on each page", () => {
    const support = reasonsOn(mount(SupportPage, options()));
    const complaint = reasonsOn(mount(ComplaintPage, options()));

    expect(support.length).toBeGreaterThan(0);
    expect(complaint.length).toBeGreaterThan(0);
    expect(support.filter((value) => complaint.includes(value))).toEqual([]);
  });

  it("offers no suggestion: ideas go to the forum", () => {
    expect(reasonsOn(mount(ComplaintPage, options()))).not.toContain(
      "SiteImprovementSuggestion",
    );
    expect(reasonsOn(mount(SupportPage, options()))).not.toContain(
      "SiteImprovementSuggestion",
    );
  });

  /**
   * Nothing is chosen for the reader: the error used to be, and whoever did
   * not open the list filed his question as an error. The fields wait for the
   * reason, so the note under it is read before anything is written.
   */
  it.each([
    ["support", SupportPage],
    ["complaint", ComplaintPage],
  ])("opens %s on no reason, with no field under it", (_, Page) => {
    const wrapper = mount(Page, options());

    expect(chosenReason(wrapper)).toBe("");
    expect(wrapper.find("select option[value='']").text()).toBe(
      "Выберите причину",
    );
    expect(labelsOn(wrapper)).toHaveLength(1);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find("button[type='submit']").exists()).toBe(false);
  });

  it("brings the fields once a reason is chosen", async () => {
    const wrapper = mount(SupportPage, options());

    await wrapper.find("select").setValue("Bug");

    expect(labelsOn(wrapper)).toEqual([
      "Причина обращения",
      "Контактные данные",
      "Описание",
    ]);
  });

  it("asks the guest for the reason, an address and the text", () => {
    expect(labelsOn(openOn(SupportPage, "Bug"))).toEqual([
      "Причина обращения",
      "Контактные данные",
      "Описание",
    ]);
    expect(
      labelsOn(openOn(ComplaintPage, "SeniorModeratorDecisionComplaint")),
    ).toEqual([
      "Причина жалобы",
      "Контактные данные",
      "Ссылка на решение",
      "Описание",
    ]);
  });

  it("asks for no subject and sends none", async () => {
    const wrapper = openOn(SupportPage, "Bug");
    expect(wrapper.find("#subject").exists()).toBe(false);

    await fillAndSend(wrapper);

    expect(createTicket).toHaveBeenCalledTimes(1);
    expect(createTicket.mock.calls[0][0]).not.toHaveProperty("subject");
  });

  it("takes the text in the site's markup editor and sends it as written", async () => {
    const wrapper = openOn(SupportPage, "Bug");
    const editor = wrapper.findComponent(BBCodeEditor);

    expect(editor.exists()).toBe(true);
    expect(editor.props("draftKey")).toBe(
      composerDraftKey("support", "ticket"),
    );

    await fillAndSend(wrapper, "Кнопка [b]Отправить[/b] не нажимается");

    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({
        text: "Кнопка [b]Отправить[/b] не нажимается",
      }),
      expect.anything(),
    );
  });

  it.each([
    ["support", SupportPage, "Bug"],
    ["complaint", ComplaintPage, "ModeratorDecisionComplaint"],
  ])(
    "gives a signed-in author on %s his name in the guest's field, which he cannot change",
    async (_, Page, subtype) => {
      signIn("Reader");
      const wrapper = openOn(Page, subtype);
      await flushPromises();

      // Not the guest's address field, and no address asked for.
      expect(wrapper.find("#contact").exists()).toBe(false);
      expect(labelsOn(wrapper)).toContain("Контактные данные");

      // The same control as the guest's: a line field under the same caption,
      // holding the name. Read-only and not disabled, so the value is read out
      // with the caption, selected and copied, and only typing is refused.
      const field = wrapper.get(".form-field-row input#sender");
      const input = field.element as HTMLInputElement;
      expect(input.type).toBe("text");
      expect(input.value).toBe("Reader");
      expect(input.readOnly).toBe(true);
      expect(input.disabled).toBe(false);
      const caption = wrapper
        .findAll(".form-field-label label")
        .find((label) => label.text() === "Контактные данные");
      expect(caption?.attributes("for")).toBe("sender");

      await fillAndSend(wrapper);

      expect(createTicket.mock.calls[0][0].contact).toBeUndefined();
    },
  );

  // DM2 asked for "email для обратной связи". DM3 sends no letter about a
  // ticket and tells the guest so after sending, so the hint keeps the old
  // words without the promise of an answer by mail.
  it("asks the guest for an address without promising a letter", () => {
    expect(hintOf(openOn(SupportPage, "Bug"), "contact")).toBe("Укажите почту");
  });

  it("sends the guest's address and holds a malformed one back", async () => {
    const wrapper = openOn(SupportPage, "Bug");
    await wrapper.find("#contact").setValue("reader@");
    await write(wrapper, "Не могу войти");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(createTicket).not.toHaveBeenCalled();
    expect(wrapper.find(".form-field-error").text()).toBe(
      "Неверный формат почты",
    );

    await wrapper.find("#contact").setValue("reader@example.com");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({ contact: "reader@example.com" }),
      expect.anything(),
    );
  });

  it("says under the editor that the text is missing", async () => {
    const wrapper = openOn(SupportPage, "Bug");
    await wrapper.find("#contact").setValue("reader@example.com");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(createTicket).not.toHaveBeenCalled();
    expect(wrapper.findAll(".form-field-error").map((e) => e.text())).toEqual([
      "Обязательное поле",
    ]);
  });

  it("claims the refusal and prints it over the button", async () => {
    createTicket.mockResolvedValue({
      data: null,
      error: { type: "", title: "Ошибка сервера", status: 500, traceId: "" },
    });
    const wrapper = openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(createTicket).toHaveBeenCalledWith(expect.anything(), {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    const line = wrapper.find(".ticket-refusal");
    expect(line.attributes("role")).toBe("alert");
    expect(line.text()).toBe("Ошибка сервера");
    // Still there once the form is unlocked after the answer.
    await flushPromises();
    expect(wrapper.find(".ticket-refusal").exists()).toBe(true);
  });

  it("puts the server's word about the text under the editor", async () => {
    createTicket.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Некорректные данные",
        status: 400,
        traceId: "",
        errors: { Text: ["Long"] },
      },
    });
    const wrapper = openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find(".form-field-error").text()).toBe(
      "Слишком длинное значение",
    );
    expect(wrapper.find(".ticket-refusal").exists()).toBe(false);
  });

  it("counts a rate limit down in the form and shuts the button", async () => {
    createTicket.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "",
        status: 429,
        traceId: "",
        retryAfterSeconds: 30,
      },
    });
    const wrapper = openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find(".ticket-refusal").text()).toContain("30");
    expect(
      wrapper.find("button[type='submit']").attributes("disabled"),
    ).toBeDefined();
  });

  // One sentence with the address of the ticket's page in it as an ordinary
  // link: no button that copies it and no line with the number to keep.
  it("gives the guest the address of his ticket after sending", async () => {
    const wrapper = openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find("form").exists()).toBe(false);
    expect(wrapper.find(".ticket-sent").text()).toContain(
      "Обращение отправлено",
    );
    expect(wrapper.get(".ticket-sent-text").text().replace(/\s+/g, " ")).toBe(
      `Ответ появится на странице обращения: ${window.location.origin}/support/track/T-1. Сохраните ссылку, чтобы вернуться к обращению.`,
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "support-track", params: { token: "T-1" } },
      text: `${window.location.origin}/support/track/T-1`,
    });
    expect(wrapper.get(".ticket-sent").text()).not.toMatch(
      /Номер обращения|Скопировать/,
    );
    // The one button left on the card opens a new form.
    expect(
      wrapper
        .findAll(".ticket-sent button")
        .map((button) => button.classes("again-button")),
    ).toEqual([true]);
  });

  // The answer is a comment of the ticket's conversation, on its own page
  // (owner, 12.10, line 52), so the card leads there and not to the list.
  it("sends a signed-in author to the page of the ticket after sending", async () => {
    signIn();
    createTicket.mockResolvedValue({
      data: { ticketId: "t-9", trackingToken: null },
      error: null,
    });
    const wrapper = openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find(".ticket-sent-text").text().replace(/\s+/g, " ")).toBe(
      "Ответ появится на странице обращения",
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "my-ticket", params: { ticketId: "t-9" } },
      text: "странице обращения",
    });
  });

  // The server takes a ticket of 10000 characters at most, and the counter
  // under the editor counts to the same number the conversation of the
  // ticket counts to.
  it("counts the text to the length the server takes a ticket at", () => {
    const wrapper = openOn(SupportPage, "Bug");

    expect(wrapper.findComponent(BBCodeEditor).props("maxLength")).toBe(
      TICKET_TEXT_MAX_LENGTH,
    );
  });

  it("opens a new form, on the same reason, after sending", async () => {
    const wrapper = openOn(SupportPage, "AccessRecovery");
    await fillAndSend(wrapper);

    await wrapper.find(".again-button").trigger("click");

    expect(wrapper.find("form").exists()).toBe(true);
    expect(chosenReason(wrapper)).toBe("AccessRecovery");
    expect(wrapper.findComponent(BBCodeEditor).props("modelValue")).toBe("");
  });

  it("keeps the reason the reader picked for the next ticket", async () => {
    const wrapper = mount(SupportPage, options());
    await wrapper.find("select").setValue("PersonalData");
    await fillAndSend(wrapper);

    await wrapper.find(".again-button").trigger("click");

    expect(chosenReason(wrapper)).toBe("PersonalData");
  });

  it("offers removal and restoration among the support reasons", () => {
    // Both are ordinary tickets - the account itself is removed and brought
    // back by moderation, by hand - so the only place they appear is this list.
    const support = reasonsOn(mount(SupportPage, options()));

    expect(support).toContain("AccountRemoval");
    expect(support).toContain("AccountRestoration");
  });

  it("preselects the reason the address names", () => {
    expect(chosenReason(openOn(SupportPage, "AccountRestoration"))).toBe(
      "AccountRestoration",
    );
  });

  it("chooses nothing for a reason the page cannot offer", () => {
    // A stale link, a typo or a reason that belongs to the other page. Taken
    // verbatim it would send a category the page never showed.
    expect(chosenReason(openOn(SupportPage, "UserComplaint"))).toBe("");
    expect(chosenReason(openOn(SupportPage, "СовсемНеПодтип"))).toBe("");
  });

  it("files the ticket under the reason the address named", async () => {
    signIn();

    await fillAndSend(openOn(SupportPage, "AccountRemoval"));

    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({ subtype: "AccountRemoval" }),
      expect.anything(),
    );
  });

  it("lets the reader change the reason the address named", async () => {
    const wrapper = openOn(SupportPage, "AccountRestoration");

    await wrapper.find("select").setValue("RegistrationIssue");
    expect(chosenReason(wrapper)).toBe("RegistrationIssue");

    // Sent as well: the picker and the payload are the same value only while
    // the reason is the form's own state and not a reading of the address.
    await fillAndSend(wrapper);
    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({ subtype: "RegistrationIssue" }),
      expect.anything(),
    );
  });

  it("asks about an error in the old site's words", () => {
    // The note the old error report printed under its button: the page the
    // error was seen on, and how to make it happen again.
    expect(openOn(SupportPage, "Bug").find(".reason-hint").text()).toBe(
      "Пожалуйста, скопируйте адрес страницы, на которой была замечена ошибка, а также постарайтесь указать, как можно воспроизвести ошибку",
    );
  });

  it("tells the reader what the chosen reason needs in the text", async () => {
    const wrapper = openOn(SupportPage, "AccountRestoration");
    const restoration = wrapper.find(".reason-hint").text();

    await wrapper.find("select").setValue("Bug");

    expect(restoration).toContain("удаленного аккаунта");
    expect(wrapper.find(".reason-hint").text()).not.toBe(restoration);
  });

  // The privacy policy and the user agreement send their reader here, and a
  // question about one's data or about the agreement is neither a bug nor an
  // account: each has a reason of its own, and there is no "other".
  it.each([
    [
      "PersonalData",
      "Персональные данные",
      "Напишите, что сделать с вашими данными: показать, исправить, удалить или прекратить обработку",
    ],
    [
      "AgreementQuestion",
      "Вопрос по соглашению",
      "Напишите, какого пункта соглашения касается вопрос",
    ],
  ])("offers %s under its own name and hint", (subtype, label, hint) => {
    const wrapper = openOn(SupportPage, subtype);

    expect(chosenReason(wrapper)).toBe(subtype);
    expect(
      wrapper.find(`option[value="${subtype}"]`).element.textContent?.trim(),
    ).toBe(label);
    expect(wrapper.find(".reason-hint").text()).toBe(hint);
    expect(wrapper.find(".reason-note").exists()).toBe(false);
  });

  it("leaves the autofill of the contact field to the field", () => {
    const wrapper = openOn(SupportPage, "Bug");

    // A form-level "off" is read by part of the browsers as a veto over the
    // fields' own values, and this is the field a password manager fills.
    expect(wrapper.find("form").attributes("autocomplete")).toBeUndefined();
    expect(wrapper.find("#contact").attributes("autocomplete")).toBe("email");
  });
});

describe("the note under a reason", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  /**
   * The reader who forgot a password or lost the phone does not need the
   * administration, and the note says so before he writes: the windows that
   * do it open over this page, and the form stays where it is. Whoever lost
   * the mailbox reads first that he came to the right place.
   */
  it("gives a guest who lost access the two windows that give it back", () => {
    const wrapper = openOn(SupportPage, "AccessRecovery");

    expect(noteOn(wrapper)).toEqual([
      "Сюда пишут, если нет доступа к почте аккаунта",
      "Забыли пароль или не пришло письмо активации: Восстановление доступа пришлет ссылку на почту",
      "Нет доступа к приложению и резервным кодам: Запрос на отключение пришлет письмо, ссылка из него отключит двухфакторную аутентификацию через 7 дней",
    ]);
    expect(wrapper.find(".reason-note-line.is-lead").exists()).toBe(true);

    expect(noteLinksOn(wrapper)).toEqual([
      { query: { subtype: "AccessRecovery", action: "recovery" } },
      { query: { subtype: "AccessRecovery", action: "two-factor" } },
    ]);
  });

  /**
   * Asked for what only the owner knows: without the mailbox, that is how the
   * administration tells the owner from somebody who knows his name.
   */
  it("asks a guest who lost access for what only the owner knows", () => {
    expect(
      openOn(SupportPage, "AccessRecovery").find(".reason-hint").text(),
    ).toBe(
      "Напишите имя пользователя и почту аккаунта, а также то, что знает только владелец: названия ваших игр и персонажей, примерную дату регистрации",
    );
  });

  it("sends a signed-in reader who lost access to his settings", () => {
    signIn();
    const wrapper = openOn(SupportPage, "AccessRecovery");

    expect(noteOn(wrapper)[0]).toBe("Вы уже вошли в аккаунт");
    expect(noteLinksOn(wrapper)).toEqual([{ name: "account" }]);
  });

  it("says what the site already sends again for a registration", () => {
    const wrapper = openOn(SupportPage, "RegistrationIssue");

    expect(noteOn(wrapper)[1]).toBe(
      'Письмо могло попасть в папку "Спам", отправить его еще раз можно через Восстановление доступа',
    );
  });

  it("tells a signed-in reader what a removal leaves behind", () => {
    signIn();

    expect(noteOn(openOn(SupportPage, "AccountRemoval"))).toEqual([
      'Посты, комментарии и сообщения останутся на месте, вместо имени над ними будет "Аккаунт удален"',
      'Вернуть имя можно через поддержку, подробнее на странице "Аккаунт удален"',
    ]);
  });

  it("says who the senior moderators are and what rudeness costs, under a decision complaint", () => {
    const wrapper = openOn(ComplaintPage, "ModeratorDecisionComplaint");

    expect(noteOn(wrapper)).toEqual([
      'Кто из модераторов старший, видно в разделе "Администрация" правил',
      "Решения по жалобам принимаются коллегиально, хамство в жалобе добавляет баллы",
    ]);
    expect(noteLinksOn(wrapper)).toEqual([{ name: "rules", hash: "#admins" }]);
  });
});

describe("the reasons filed from an account", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
    createTicket.mockReset();
    createTicket.mockResolvedValue({ data: {}, error: null });
  });

  /**
   * As on DM2, where a guest read "Для подачи жалобы требуется авторизация":
   * the site's sign-in line in place of the fields, and where to go for a
   * reader whose account is banned and cannot sign in.
   */
  it.each([
    [
      ComplaintPage,
      "UserComplaint",
      "пожаловаться на пользователя",
      [
        "Жалобу на пользователя через форму подают из аккаунта",
        "Войдите, чтобы пожаловаться на пользователя",
        'Аккаунт в бане: выберите причину "Жалоба на решение старшего модератора"',
      ],
    ],
    [
      SupportPage,
      "AccountRemoval",
      "запросить удаление аккаунта",
      [
        "Удаляем аккаунт только по просьбе из него самого",
        "Войдите, чтобы запросить удаление аккаунта",
        'Не получается войти: выберите причину "Восстановление доступа"',
      ],
    ],
  ])(
    "gives a guest the sign-in line instead of the form for %s",
    (Page, subtype, action, lines) => {
      const wrapper = openOn(Page as Page, subtype as string);

      expect(noteOn(wrapper)).toEqual(lines);
      expect(wrapper.findComponent(LoginPrompt).props("action")).toBe(action);
      expect(labelsOn(wrapper)).toHaveLength(1);
      expect(wrapper.find("button[type='submit']").exists()).toBe(false);
    },
  );

  it("asks a member for the violator and the link, both required", async () => {
    signIn();
    const wrapper = openOn(ComplaintPage, "UserComplaint");

    expect(labelsOn(wrapper)).toEqual([
      "Причина жалобы",
      "Контактные данные",
      "Нарушитель",
      "Ссылка на нарушение",
      "Описание",
    ]);
    expect(wrapper.findAll(".form-field-optional")).toHaveLength(0);
    expect(hintOf(wrapper, "violationUrl")).toBe(
      "Адрес страницы, где это случилось: пост, комментарий, топик, чат или профиль",
    );

    await fillAndSend(wrapper);

    expect(createTicket).not.toHaveBeenCalled();
    expect(wrapper.findAll(".form-field-error").map((e) => e.text())).toEqual([
      "Обязательное поле",
      "Обязательное поле",
    ]);
  });

  it("holds back a link that leads to no page", async () => {
    signIn();
    const wrapper = openOn(ComplaintPage, "UserComplaint");
    await wrapper.find("#violator").setValue("Experienced");
    await wrapper.find("#violationUrl").setValue("forum/flood/1");

    await fillAndSend(wrapper);

    expect(createTicket).not.toHaveBeenCalled();
    expect(wrapper.find(".form-field-error").text()).toBe(
      "Нужен адрес страницы, начиная с https://",
    );
  });

  it("sends the violator and the link with the complaint", async () => {
    signIn();
    const wrapper = openOn(ComplaintPage, "UserComplaint");
    await wrapper.find("#violator").setValue("  Experienced ");
    await wrapper
      .find("#violationUrl")
      .setValue("https://example.com/forum/flood/1");

    await fillAndSend(wrapper, "Оскорбляет в комментариях");

    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({
        subtype: "UserComplaint",
        targetUsername: "Experienced",
        violationUrl: "https://example.com/forum/flood/1",
      }),
      expect.anything(),
    );
  });

  it("puts the server's word about the violator under his field", async () => {
    signIn();
    createTicket.mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Некорректные данные",
        status: 400,
        traceId: "",
        errors: { targetUsername: ["Пользователь не найден"] },
      },
    });
    const wrapper = openOn(ComplaintPage, "UserComplaint");
    await wrapper.find("#violator").setValue("Nobody");
    await wrapper
      .find("#violationUrl")
      .setValue("https://example.com/forum/flood/1");

    await fillAndSend(wrapper);

    const error = wrapper.get("#violator").element.closest(".form-field");
    expect(error?.querySelector(".form-field-error")?.textContent).toBe(
      "Пользователь не найден",
    );
    expect(wrapper.find(".ticket-refusal").exists()).toBe(false);
  });

  /** A decision is found by who complains, so its link stays optional. */
  it("keeps the link of a decision complaint optional and sends no violator", async () => {
    signIn();
    const wrapper = openOn(ComplaintPage, "SeniorModeratorDecisionComplaint");

    expect(labelsOn(wrapper)).not.toContain("Нарушитель");
    expect(wrapper.find(".form-field-optional").text()).toBe("необязательно");
    expect(hintOf(wrapper, "violationUrl")).toBe(
      "Пост или комментарий, за который вынесено решение",
    );

    await fillAndSend(wrapper);

    expect(createTicket.mock.calls[0][0].targetUsername).toBeUndefined();
    expect(createTicket.mock.calls[0][0].violationUrl).toBeUndefined();
  });

  it("asks a banned guest for the name of his account in a decision complaint", () => {
    expect(
      openOn(ComplaintPage, "SeniorModeratorDecisionComplaint")
        .find(".reason-hint")
        .text(),
    ).toBe(
      "Напишите имя пользователя аккаунта, какое решение оспариваете и почему не согласны",
    );
  });
});

describe("the support page", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  /**
   * The windows open over the page: the note's links change the query and
   * nothing else, so the reader who closes the window is still on the form.
   * The card the page used to carry, "Перейти к восстановлению доступа", led
   * away from it.
   */
  it("opens the ways back to an account over the page, not away from it", () => {
    const wrapper = openOn(SupportPage, "AccessRecovery");

    expect(wrapper.text()).not.toMatch(/Перейти/);
    for (const link of linksOn(wrapper).filter(
      (entry) =>
        typeof entry.to === "object" &&
        (entry.to as { query?: Record<string, string> }).query?.action,
    )) {
      expect(Object.keys(link.to as object)).toEqual(["query"]);
    }
  });

  it("sends suggestions and discussions to the forum", () => {
    expect(linksOn(mount(SupportPage, options()))).toContainEqual({
      to: { name: "forum", params: { alias: "improvements" } },
      text: "Улучшение сайта",
    });
  });

  it("sends every other error to the forum's errors section", () => {
    // The old site's page: only security errors and trouble with access come
    // here, the rest is a topic where the community answers.
    const wrapper = mount(SupportPage, options());

    expect(wrapper.find(".lead-text").text()).toContain(
      "Это сообщение отправится только администрации сайта",
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "forum", params: { alias: "bugs" } },
      text: "Ошибки",
    });
  });

  it("sends a question about how the site works to the mentors and the newcomers", () => {
    const links = linksOn(mount(SupportPage, options()));

    expect(links).toContainEqual({
      to: { name: "community", query: { role: "Mentor" } },
      text: "наставникам",
    });
    expect(links).toContainEqual({
      to: { name: "forum", params: { alias: "newbies" } },
      text: "Для новичков",
    });
  });
});

describe("the complaint page", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  it("says what is a violation, with the table of them one link away", () => {
    const wrapper = mount(ComplaintPage, options());

    expect(wrapper.find(".lead-text").text()).toContain(
      "Жалобу разбирают модераторы",
    );
    expect(linksOn(wrapper)).toContainEqual({
      to: { name: "rules", hash: "#penalties" },
      text: "правила сайта",
    });
  });

  it("says a master's decision in his game is not one", () => {
    expect(mount(ComplaintPage, options()).find(".complaint-not").text()).toBe(
      "Решения мастера в его игре нарушением не считаются: подаваясь в игру, игроки принимают его условия",
    );
  });
});
