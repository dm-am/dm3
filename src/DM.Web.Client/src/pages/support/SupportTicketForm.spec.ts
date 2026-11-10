/**
 * @vitest-environment jsdom
 */

// Support and complaint: two pages over one form. The reason comes first, so
// the note under it is read before anything is written and moderation is not
// sent what it cannot help with. The pages are mounted, not the form alone:
// a page that grew a second copy of the form must fail.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import SupportPage from "./SupportPage.vue";
import ComplaintPage from "./ComplaintPage.vue";
import { SupportTicketForm } from "@/features/support-ticket";
import { TICKET_TEXT_MAX_LENGTH } from "@/entities/ticket";
import { createTicket } from "./SupportTicketForm.doubles";
import {
  chosenReason,
  fillAndSend,
  hintOf,
  labelsOn,
  linksOn,
  openOn,
  reasonsOn,
  signIn,
  stubResizeObserver,
  write,
} from "./SupportTicketForm.fixtures";

vi.mock("@/entities/ticket", async (importOriginal) =>
  (await import("./SupportTicketForm.doubles")).ticketEntity(importOriginal),
);

// Left mounted, the tiptap editor's timers fire after the environment is torn down.
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("support and complaint", () => {
  beforeEach(() => {
    // No stored viewer: the guest wording is what both pages open with.
    localStorage.clear();
    // A bare address, as a reader who typed /support by hand arrives with.
    createTicket.mockReset();
    createTicket.mockResolvedValue({
      data: { trackingToken: "T-1" },
      error: null,
    });
  });

  it("mounts one and the same form on both pages", async () => {
    expect(
      (await openOn(SupportPage)).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
    expect(
      (await openOn(ComplaintPage)).findComponent(SupportTicketForm).exists(),
    ).toBe(true);
  });

  it("offers a different list of reasons on each page", async () => {
    const support = reasonsOn(await openOn(SupportPage));
    const complaint = reasonsOn(await openOn(ComplaintPage));

    expect(support.length).toBeGreaterThan(0);
    expect(complaint.length).toBeGreaterThan(0);
    expect(support.filter((value) => complaint.includes(value))).toEqual([]);
  });

  // Nothing is chosen for the reader: the fields wait for the reason.
  it.each([
    ["support", SupportPage],
    ["complaint", ComplaintPage],
  ])("opens %s on no reason, with no field under it", async (_, Page) => {
    const wrapper = await openOn(Page);

    expect(chosenReason(wrapper)).toBe("");
    expect(wrapper.find("select option[value='']").text()).toBe(
      "Выберите причину",
    );
    expect(labelsOn(wrapper)).toHaveLength(1);
    expect(wrapper.findComponent(BBCodeEditor).exists()).toBe(false);
    expect(wrapper.find("button[type='submit']").exists()).toBe(false);
  });

  it("brings the fields once a reason is chosen", async () => {
    const wrapper = await openOn(SupportPage);

    await wrapper.find("select").setValue("Bug");

    expect(labelsOn(wrapper)).toEqual([
      "Причина обращения",
      "Контактные данные",
      "Описание",
    ]);
  });

  it("asks the guest for the reason, an address and the text", async () => {
    expect(labelsOn(await openOn(SupportPage, "Bug"))).toEqual([
      "Причина обращения",
      "Контактные данные",
      "Описание",
    ]);
    expect(
      labelsOn(await openOn(ComplaintPage, "SeniorModeratorDecisionComplaint")),
    ).toEqual([
      "Причина жалобы",
      "Контактные данные",
      "Ссылка на решение",
      "Описание",
    ]);
  });

  it("asks for no subject and sends none", async () => {
    const wrapper = await openOn(SupportPage, "Bug");
    expect(wrapper.find("#subject").exists()).toBe(false);

    await fillAndSend(wrapper);

    expect(createTicket).toHaveBeenCalledTimes(1);
    expect(createTicket.mock.calls[0][0]).not.toHaveProperty("subject");
  });

  it("takes the text in the site's markup editor and sends it as written", async () => {
    const wrapper = await openOn(SupportPage, "Bug");
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
      const wrapper = await openOn(Page, subtype);
      await flushPromises();

      // Not the guest's address field, and no address asked for.
      expect(wrapper.find("#contact").exists()).toBe(false);
      expect(labelsOn(wrapper)).toContain("Контактные данные");

      // Read-only and not disabled: the value is read out, selected and copied.
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

  // DM3 sends no letter about a ticket, so the hint promises none.
  it("asks the guest for an address without promising a letter", async () => {
    expect(hintOf(await openOn(SupportPage, "Bug"), "contact")).toBe(
      "Укажите почту",
    );
  });

  it("sends the guest's address and holds a malformed one back", async () => {
    const wrapper = await openOn(SupportPage, "Bug");
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
    const wrapper = await openOn(SupportPage, "Bug");
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
    const wrapper = await openOn(SupportPage, "Bug");

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
    const wrapper = await openOn(SupportPage, "Bug");

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
    const wrapper = await openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find(".ticket-refusal").text()).toContain("30");
    expect(
      wrapper.find("button[type='submit']").attributes("disabled"),
    ).toBeDefined();
  });

  // One sentence with the address of the ticket's page as an ordinary link.
  it("gives the guest the address of his ticket after sending", async () => {
    const wrapper = await openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find("form").exists()).toBe(false);
    expect(wrapper.find(".ticket-sent").text()).toContain(
      "Обращение отправлено",
    );
    expect(wrapper.get(".ticket-sent-text").text().replace(/\s+/g, " ")).toBe(
      `Ответ появится на странице обращения: ${window.location.origin}/support/track#token=T-1. Сохраните ссылку, чтобы вернуться к обращению.`,
    );
    expect(linksOn(wrapper)).toContainEqual({
      href: "/support/track#token=T-1",
      text: `${window.location.origin}/support/track#token=T-1`,
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

  // The answer is a comment on the ticket's page, so the card leads there.
  it("sends a signed-in author to the page of the ticket after sending", async () => {
    signIn();
    createTicket.mockResolvedValue({
      data: { ticketId: "t-9", trackingToken: null },
      error: null,
    });
    const wrapper = await openOn(SupportPage, "Bug");

    await fillAndSend(wrapper);

    expect(wrapper.find(".ticket-sent-text").text().replace(/\s+/g, " ")).toBe(
      "Ответ появится на странице обращения",
    );
    expect(linksOn(wrapper)).toContainEqual({
      href: "/my-tickets/t-9",
      text: "странице обращения",
    });
  });

  // The counter counts to the number the server takes and the conversation counts to.
  it("counts the text to the length the server takes a ticket at", async () => {
    const wrapper = await openOn(SupportPage, "Bug");

    expect(wrapper.findComponent(BBCodeEditor).props("maxLength")).toBe(
      TICKET_TEXT_MAX_LENGTH,
    );
  });

  it("opens a new form, on the same reason, after sending", async () => {
    const wrapper = await openOn(SupportPage, "AccessRecovery");
    await fillAndSend(wrapper);

    await wrapper.find(".again-button").trigger("click");

    expect(wrapper.find("form").exists()).toBe(true);
    expect(chosenReason(wrapper)).toBe("AccessRecovery");
    expect(wrapper.findComponent(BBCodeEditor).props("modelValue")).toBe("");
  });

  it("keeps the reason the reader picked for the next ticket", async () => {
    const wrapper = await openOn(SupportPage);
    await wrapper.find("select").setValue("PersonalData");
    await fillAndSend(wrapper);

    await wrapper.find(".again-button").trigger("click");

    expect(chosenReason(wrapper)).toBe("PersonalData");
  });

  it("offers removal and restoration among the support reasons", async () => {
    // Removal and restoration are done by moderation by hand: this list is their only place.
    const support = reasonsOn(await openOn(SupportPage));

    expect(support).toContain("AccountRemoval");
    expect(support).toContain("AccountRestoration");
  });

  it("preselects the reason the address names", async () => {
    expect(chosenReason(await openOn(SupportPage, "AccountRestoration"))).toBe(
      "AccountRestoration",
    );
  });

  it("chooses nothing for a reason the page cannot offer", async () => {
    // A stale link or a typo would otherwise send a category the page never showed.
    expect(chosenReason(await openOn(SupportPage, "UserComplaint"))).toBe("");
    expect(chosenReason(await openOn(SupportPage, "СовсемНеПодтип"))).toBe("");
  });

  it("files the ticket under the reason the address named", async () => {
    signIn();

    await fillAndSend(await openOn(SupportPage, "AccountRemoval"));

    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({ subtype: "AccountRemoval" }),
      expect.anything(),
    );
  });

  it("lets the reader change the reason the address named", async () => {
    const wrapper = await openOn(SupportPage, "AccountRestoration");

    await wrapper.find("select").setValue("RegistrationIssue");
    expect(chosenReason(wrapper)).toBe("RegistrationIssue");

    // The payload, not only the picker: the reason is the form's own state.
    await fillAndSend(wrapper);
    expect(createTicket).toHaveBeenCalledWith(
      expect.objectContaining({ subtype: "RegistrationIssue" }),
      expect.anything(),
    );
  });

  it("asks about an error in the old site's words", async () => {
    // The note the old error report printed under its button.
    expect((await openOn(SupportPage, "Bug")).find(".reason-hint").text()).toBe(
      "Пожалуйста, скопируйте адрес страницы, на которой была замечена ошибка, а также постарайтесь указать, как можно воспроизвести ошибку",
    );
  });

  it("tells the reader what the chosen reason needs in the text", async () => {
    const wrapper = await openOn(SupportPage, "AccountRestoration");
    const restoration = wrapper.find(".reason-hint").text();

    await wrapper.find("select").setValue("Bug");

    expect(restoration).toContain("удаленного аккаунта");
    expect(wrapper.find(".reason-hint").text()).not.toBe(restoration);
  });

  // Data and agreement questions each have a reason of their own; there is no "other".
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
  ])("offers %s under its own name and hint", async (subtype, label, hint) => {
    const wrapper = await openOn(SupportPage, subtype);

    expect(chosenReason(wrapper)).toBe(subtype);
    expect(
      wrapper.find(`option[value="${subtype}"]`).element.textContent?.trim(),
    ).toBe(label);
    expect(wrapper.find(".reason-hint").text()).toBe(hint);
    expect(wrapper.find(".reason-note").exists()).toBe(false);
  });

  it("leaves the autofill of the contact field to the field", async () => {
    const wrapper = await openOn(SupportPage, "Bug");

    // A form-level "off" reads to some browsers as a veto over a password manager's fill.
    expect(wrapper.find("form").attributes("autocomplete")).toBeUndefined();
    expect(wrapper.find("#contact").attributes("autocomplete")).toBe("email");
  });
});
