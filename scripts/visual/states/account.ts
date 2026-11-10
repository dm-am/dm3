import {
  accountSection,
  answer,
  button,
  refuse,
  signIn,
  type State,
} from "./shared";

export const ACCOUNT_STATES: State[] = [
  // Account
  {
    // Another number of rows on a page, and the save refused.
    name: "member-account-settings-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/users\/me\/preferences/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      const pageSize = page.locator("#account-page-size");
      await pageSize.selectOption(
        (await pageSize.inputValue()) === "20" ? "30" : "20",
      );
      await accountSection(page, "Личные настройки")
        .getByRole("button", { name: "Сохранить" })
        .click();
    },
  },
  {
    // The own settings changed and not saved: another number of rows on a
    // page, "Сохранить" open. Nothing is sent.
    name: "member-account-settings-changed",
    role: "user",
    url: "/account",
    act: async (page) => {
      const section = accountSection(page, "Личные настройки");
      const pageSize = section.locator("#account-page-size");
      await pageSize.selectOption(
        (await pageSize.inputValue()) === "20" ? "30" : "20",
      );
    },
  },
  {
    name: "member-account-password-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/account\/password/,
        "POST",
        400,
        "Тестовый отказ",
      );
      await page.locator("#old-password").fill("Test123!");
      await page.locator("#new-password").fill("Another123!");
      await page.locator("#confirm-password").fill("Another123!");
      await button(page, "Изменить пароль").click();
    },
  },
  {
    // The new password typed twice and the current one left empty: the
    // button stays off.
    name: "member-account-password-empty",
    role: "user",
    url: "/account",
    act: async (page) => {
      await page.locator("#new-password").fill("Another123!");
      await page.locator("#confirm-password").fill("Another123!");
    },
  },
  {
    // The sessions of the stand's own sign-ins are folded into one, so the
    // list is answered with one more device; its "Выйти со всех других
    // устройств" is refused. "Выйти везде" is never pressed.
    name: "member-account-sessions-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      const session = (n: number, isCurrent: boolean) => ({
        id: `00000000-0000-0000-0000-0000000000e${n}`,
        createdUtc: "2026-06-01T10:00:00Z",
        expirationUtc: "2026-07-01T10:00:00Z",
        deviceInfo: isCurrent ? "Chrome, Windows" : "Firefox, Linux",
        ipAddress: isCurrent ? "127.0.0.1" : "10.0.0.2",
        isCurrent,
        persistent: true,
      });
      await answer(page, /\/v1\/account\/sessions$/, {
        resources: [session(1, true), session(2, false)],
        paging: null,
      });
      await refuse(
        page,
        /\/v1\/account\/sessions\/others/,
        "DELETE",
        400,
        "Тестовый отказ",
      );
      await page.reload({ waitUntil: "load" });
      await button(page, "Выйти со всех других устройств").click();
    },
  },
  {
    // The own name typed into the field: the field never offers it, so the
    // button stays off and the server's refusal is not reached from here.
    name: "member-account-blacklist-self",
    role: "user",
    url: "/account",
    act: async (page) => {
      await accountSection(page, "Черный список")
        .getByPlaceholder("Имя пользователя")
        .fill("TestUser");
    },
  },
  {
    // A reason shorter than the form takes: the button stays off.
    name: "member-account-username-short-reason",
    role: "user",
    url: "/account",
    act: async (page) => {
      await page.locator("#username-reason").fill("коротко");
    },
  },
  {
    // The invitations of the seed are swept a week after it was laid out:
    // the list is answered here, and so is the server's refusal to accept.
    name: "member-account-invitation-refused",
    role: "user",
    url: "/account",
    act: async (page) => {
      await answer(page, /\/v1\/users\/me\/invitations$/, {
        resources: [
          {
            id: "00000000-0000-0000-0000-0000000000a1",
            entityId: "00000000-0000-0000-0000-0000000000a2",
            entityType: "game",
            entityTitle: "Игра для кадра",
            inviterUsername: "SolohinLex",
            type: "player",
            createdUtc: "2026-01-01T00:00:00Z",
          },
        ],
        paging: null,
      });
      await refuse(
        page,
        /\/v1\/users\/me\/invitations\/[^/]+\/accept$/,
        "POST",
        404,
        "Invitation not found or already processed",
      );
      await page.reload({ waitUntil: "load" });
      await accountSection(page, "Приглашения")
        .getByRole("button", { name: "Принять" })
        .click();
    },
  },
  {
    // The pending registration of the seed; the letter is not sent again.
    name: "guest-login-pending-account",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: async (page) => {
      await signIn(page, "inactive@test.local");
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "guest-login-unknown-email",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: (page) => signIn(page, "nobody@test.local"),
  },
  {
    // LongestLoginPossible holds a permanent full ban (DataSeeder.Bans.cs).
    name: "guest-login-banned",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: (page) => signIn(page, "longest@test.local"),
  },
  {
    // The fields left empty, one after another: the button stays off.
    name: "guest-login-empty",
    role: "guest",
    url: "/?action=login",
    whole: false,
    act: async (page) => {
      await page.locator("#email").click();
      await page.keyboard.press("Tab");
      await page.locator("#password").click();
      await page.keyboard.press("Tab");
    },
  },
  {
    // The address left empty: the button stays off.
    name: "guest-registration-empty",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").click();
      await page.keyboard.press("Tab");
    },
  },
  {
    // A free address, then a weak password; the form is never sent. Asking
    // whether an address is free writes nothing (AvailabilityController).
    name: "guest-registration-weak",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").fill("newcomer@test.local");
      await button(page, "Продолжить").click();
      await page.locator("#registration-password").fill("abc");
    },
  },
  {
    name: "guest-registration-bad-email",
    role: "guest",
    url: "/?action=register",
    whole: false,
    act: async (page) => {
      await page.locator("#registration-email").fill("bad@");
      await page.keyboard.press("Tab");
    },
  },
];
