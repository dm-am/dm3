/**
 * @vitest-environment jsdom
 */

// The note under a reason, and the two reasons filed from an account (ticketReasons.ts).
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount } from "@vue/test-utils";
import { LoginPrompt } from "@/features/auth";
import SupportPage from "./SupportPage.vue";
import ComplaintPage from "./ComplaintPage.vue";
import { createTicket, route } from "./SupportTicketForm.doubles";
import {
  fillAndSend,
  hintOf,
  labelsOn,
  noteLinksOn,
  noteOn,
  openOn,
  signIn,
  stubResizeObserver,
  type Page,
} from "./SupportTicketForm.fixtures";

vi.mock("vue-router", async () =>
  (await import("./SupportTicketForm.doubles")).vueRouter(),
);
vi.mock("@/entities/ticket", async (importOriginal) =>
  (await import("./SupportTicketForm.doubles")).ticketEntity(importOriginal),
);
enableAutoUnmount(afterEach);
stubResizeObserver();

describe("the note under a reason", () => {
  beforeEach(() => {
    localStorage.clear();
    route.query = {};
  });

  // Forgetting a password or losing the phone needs no administration: the
  // windows that give access back open over the page.
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

  // Without the mailbox, what only the owner knows tells him from somebody who knows his name.
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

  // As on DM2: the sign-in line in place of the fields, and where a banned reader goes.
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
