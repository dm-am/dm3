import { button, openFilter, refuse, type State } from "./shared";

export const OTHER_PAGES_STATES: State[] = [
  // Other pages
  {
    name: "guest-polls-filter",
    role: "guest",
    url: "/polls",
    whole: false,
    act: openFilter,
  },
  {
    name: "senior-poll-create-refused",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await refuse(page, /\/v1\/polls$/, "POST", 400, "Другая причина");
      await button(page, "+ Создать опрос").click();
      await page.locator("#poll-title").fill("Тест");
      await page.getByPlaceholder("Вариант 1").fill("Да");
      await page.getByPlaceholder("Вариант 2").fill("Нет");
      await button(page, "Создать опрос").click();
      // The open form outgrew the window stretched before it opened, and the
      // refusal scrolls the page in some runs and not in others: back to the top.
      await page.mouse.wheel(0, -5000);
    },
  },
  {
    name: "senior-poll-edit",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await button(page, "Редактировать опрос").click();
    },
  },
  {
    // The senior moderator has voted in a running poll; the member has not.
    name: "senior-poll-cancel-vote",
    role: "senior",
    url: "/polls",
    act: async (page) => {
      await refuse(page, /\/v1\/polls\/[^/]+\/vote/, "DELETE", 500, "Ошибка");
      await page
        .locator("main")
        .getByRole("button", { name: "Отменить голос" })
        .first()
        .click();
    },
  },
  {
    name: "member-testimonials",
    role: "user",
    url: "/testimonials",
    act: async (page) => {
      await page.getByRole("textbox").first().fill("тест");
    },
  },
  {
    // The search over the testimonials applies on Enter.
    name: "member-testimonials-wildcard",
    role: "user",
    url: "/testimonials",
    act: async (page) => {
      const search = page.getByPlaceholder("Поиск по тексту или автору");
      await search.fill("_");
      await search.press("Enter");
    },
  },
  {
    name: "guest-rules-expanded",
    role: "guest",
    url: "/rules",
    act: async (page) => {
      await button(page, "Развернуть все").click();
    },
  },
  {
    // The staff of the rules is asked by role, and every role is refused.
    name: "guest-rules-staff-failed",
    role: "guest",
    url: "/rules",
    act: async (page) => {
      await refuse(page, /\/v1\/users\?.*role=/, "GET", 500, "Ошибка");
      await page.reload({ waitUntil: "load" });
    },
  },
  {
    name: "guest-statistics-bad-month",
    role: "guest",
    url: "/statistics?year=2026&month=13",
  },
  {
    name: "guest-privacy",
    role: "guest",
    url: "/privacy",
  },
  {
    name: "guest-agreement",
    role: "guest",
    url: "/agreement",
  },
  {
    name: "guest-donate",
    role: "guest",
    url: "/donate",
  },
];
