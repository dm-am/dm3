import { button, refuse, refuseOnLoad, type State } from "./shared";

export const MODERATION_STATES: State[] = [
  // Moderation
  {
    name: "admin-award-types-refused",
    role: "admin",
    url: "/moderation/award-types",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/moderation\/award-types\/[^/]+$/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      await button(page, /^(Скрыть|Вернуть)$/).click();
    },
  },
  {
    name: "admin-award-type-dialog",
    role: "admin",
    url: "/moderation/award-types",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    // The series of the 23rd literary contest, whose id the catalogue fixes (AwardCatalog).
    name: "admin-award-series-refused",
    role: "admin",
    url: "/moderation/awards/series/00000000-0000-0000-0004-000000000001",
    act: async (page) => {
      await refuse(
        page,
        /\/v1\/moderation\/contest-series\/[^/]+$/,
        "PATCH",
        400,
        "Тестовый отказ",
      );
      await page.getByLabel(/Активна/).click();
      await button(page, "Сохранить").click();
    },
  },
  {
    // A year the form takes and the server does not (AwardValidators.cs); nothing is sent.
    name: "admin-award-series-create-year",
    role: "admin",
    url: "/moderation/awards",
    whole: false,
    act: async (page) => {
      await button(page, "+ Новая серия").click();
      const dialog = page.getByRole("dialog");
      await dialog.waitFor();
      await dialog.getByLabel("Год").fill("2001");
    },
  },
  {
    name: "admin-achievements",
    role: "admin",
    url: "/moderation/achievements",
  },
  {
    // The catalogue loads on mount, so the refusal stands before the reload.
    name: "admin-achievements-refused",
    role: "admin",
    url: "/moderation/achievements",
    act: (page) =>
      refuseOnLoad(
        page,
        /\/v1\/moderation\/achievement-(categories|types)(\?|$)/,
      ),
  },
  {
    name: "admin-achievement-category-dialog",
    role: "admin",
    url: "/moderation/achievements",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-achievement-tier-dialog",
    role: "admin",
    url: "/moderation/achievements",
    whole: false,
    act: async (page) => {
      await button(page, /^Редактировать тир/).click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-tag-dialog",
    role: "admin",
    url: "/moderation/tags",
    whole: false,
    act: async (page) => {
      await button(page, "Редактировать тег").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-warn-dialog",
    role: "admin",
    url: "/users/RatingNegative",
    whole: false,
    act: async (page) => {
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать предупреждение за профиль").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-ban-dialog",
    role: "admin",
    url: "/users/RatingNegative",
    whole: false,
    act: async (page) => {
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать бан").click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    // Experienced is banned already, and the server refuses a second ban
    // before it writes (BanService.CreateBan). Past the end of his ban the
    // request would ban him, so it goes out only while the ban runs.
    name: "admin-ban-repeat-refused",
    role: "admin",
    url: "/users/Experienced",
    whole: false,
    act: async (page) => {
      await page.route(/\/v1\/bans$/, async (route) => {
        if (route.request().method() !== "POST") return route.fallback();
        const api = new URL(route.request().url()).origin;
        const running = await page.request.get(
          `${api}/v1/users/Experienced/bans/active`,
        );
        return running.ok() ? route.fallback() : route.abort();
      });
      await page.getByRole("tab", { name: "Модерация", exact: true }).click();
      await button(page, "Выдать бан").click();
      const dialog = page.getByRole("dialog");
      await dialog.getByRole("textbox").first().fill("тест");
      await dialog.getByRole("button", { name: /Выдать|Забанить/ }).click();
    },
  },
  {
    // The one running ban of the list that can be lifted is Experienced's.
    name: "admin-unban-dialog",
    role: "admin",
    url: "/moderation/bans",
    whole: false,
    act: async (page) => {
      await page.getByText("Снять бан", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-points-dialog",
    role: "admin",
    url: "/moderation/warnings",
    whole: false,
    act: async (page) => {
      await page.getByText("Изменить баллы", { exact: true }).first().click();
      await page.getByRole("dialog").waitFor();
    },
  },
  {
    name: "admin-fundraising",
    role: "admin",
    url: "/moderation/fundraising",
  },
  {
    name: "admin-removed-accounts-search-empty",
    role: "admin",
    url: "/moderation/removed-accounts",
    act: async (page) => {
      await page.getByPlaceholder("Имя или почта").fill("яяяя");
      await button(page, "Найти").click();
      await page.getByText("Ничего не найдено").waitFor();
    },
  },
  {
    name: "moderator-rated-posts",
    role: "moderator",
    url: "/moderation/rated-posts",
  },
];
